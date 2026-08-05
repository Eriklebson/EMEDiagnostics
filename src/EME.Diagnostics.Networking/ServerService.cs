using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EME.Diagnostics.Networking.Models;

namespace EME.Diagnostics.Networking;

public sealed class ServerService : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly List<RemoteMachineInfo> _clients = [];
    private readonly List<RemoteReportInfo> _reports = [];
    private readonly object _lock = new();

    public int Port { get; } = NetworkConstants.ServerPort;
    public bool IsRunning => _listener?.IsListening ?? false;

    public IReadOnlyList<RemoteMachineInfo> Clients
    {
        get { lock (_lock) return _clients.ToList().AsReadOnly(); }
    }

    public IReadOnlyList<RemoteReportInfo> Reports
    {
        get { lock (_lock) return _reports.ToList().AsReadOnly(); }
    }

    public event EventHandler? ClientsChanged;
    public event EventHandler? ReportsChanged;

    public string ReportsDirectory { get; }
    public string ReportsIndexPath { get; }

    public ServerService()
    {
        ReportsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EME", "Diagnostics", "network_reports");
        Directory.CreateDirectory(ReportsDirectory);
        ReportsIndexPath = Path.Combine(ReportsDirectory, "reports_index.json");

        MigrateLegacyReports();
        LoadPersistedReports();
        EnsureFirewallRule();
    }

    private void MigrateLegacyReports()
    {
        try
        {
            var legacyDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EMEDiagnostics", "network_reports");
            if (!Directory.Exists(legacyDirectory) ||
                string.Equals(legacyDirectory, ReportsDirectory, StringComparison.OrdinalIgnoreCase)) return;

            foreach (var sourcePath in Directory.EnumerateFiles(legacyDirectory))
            {
                var destinationPath = Path.Combine(ReportsDirectory, Path.GetFileName(sourcePath));
                if (!File.Exists(destinationPath)) File.Copy(sourcePath, destinationPath);
            }
            NetDiagnostics.Log($"Legacy network reports migrated from {legacyDirectory} to {ReportsDirectory}");
        }
        catch (Exception ex)
        {
            NetDiagnostics.Log($"Failed to migrate legacy reports: {ex.Message}");
        }
    }

    private void LoadPersistedReports()
    {
        try
        {
            if (!File.Exists(ReportsIndexPath)) return;

            var json = File.ReadAllText(ReportsIndexPath);
            var persisted = JsonSerializer.Deserialize<List<RemoteReportInfo>>(json, JsonOpts);
            if (persisted == null) return;

            lock (_lock)
            {
                _reports.Clear();
                _reports.AddRange(persisted);
                RemoveOrphanedReports();
            }
            NetDiagnostics.Log($"Loaded {_reports.Count} persisted reports from index");
        }
        catch (Exception ex)
        {
            NetDiagnostics.Log($"Failed to load persisted reports: {ex.Message}");
        }
    }

    private void RemoveOrphanedReports()
    {
        for (var i = _reports.Count - 1; i >= 0; i--)
        {
            if (GetReportFile(_reports[i].Id) == null)
                _reports.RemoveAt(i);
        }
    }

    private void PersistReports()
    {
        try
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(_reports, JsonOpts);
                File.WriteAllText(ReportsIndexPath, json);
            }
        }
        catch (Exception ex)
        {
            NetDiagnostics.Log($"Failed to persist reports: {ex.Message}");
        }
    }

    private string? GetReportFile(string reportId)
    {
        var files = Directory.GetFiles(ReportsDirectory, $"{reportId}_*.pdf");
        return files.FirstOrDefault();
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning)
        {
            NetDiagnostics.Log("Server already running, ignoring StartAsync");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{Port}/");
        _listener.Start();
        NetDiagnostics.Log($"Server started on port {Port}");

        _ = Task.Run(() => AdvertiseLoop(_cts.Token), _cts.Token);
        _ = Task.Run(() => AcceptLoop(_cts.Token), _cts.Token);

        await Task.CompletedTask;
    }

    public void Stop()
    {
        NetDiagnostics.Log("Server stopping...");
        _cts?.Cancel();
        _listener?.Stop();
        NetDiagnostics.Log("Server stopped");
    }

    private async Task AdvertiseLoop(CancellationToken ct)
    {
        var hostName = Environment.MachineName;
        var message = $"{NetworkConstants.AdvertisementMessage}|{hostName}|{Port}";
        var bytes = Encoding.UTF8.GetBytes(message);

        var adapters = GetBroadcastAdapters();
        NetDiagnostics.Log($"Broadcast adapters: {adapters.Count} ({string.Join(", ", adapters.Select(a => $"{a.LocalIp} -> {a.BroadcastAddress}"))})");

        var sockets = new List<UdpClient>();
        foreach (var adapter in adapters)
        {
            try
            {
                var udp = new UdpClient(new IPEndPoint(adapter.LocalIp, 0)) { EnableBroadcast = true };
                sockets.Add(udp);
            }
            catch (Exception ex)
            {
                NetDiagnostics.Log($"Failed to bind socket for {adapter.LocalIp}: {ex.Message}");
            }
        }

        if (sockets.Count == 0)
        {
            NetDiagnostics.Log("No valid network interface to broadcast on");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                for (var i = 0; i < sockets.Count; i++)
                {
                    try
                    {
                        await sockets[i].SendAsync(bytes, bytes.Length, adapters[i].BroadcastAddress);
                    }
                    catch (Exception ex)
                    {
                        NetDiagnostics.Log($"Send on {adapters[i].LocalIp} failed: {ex.Message}");
                    }
                }
                await Task.Delay(NetworkConstants.HeartbeatIntervalMs, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { break; }
        }

        foreach (var udp in sockets) udp.Close();
    }

    private static List<(IPAddress LocalIp, IPEndPoint BroadcastAddress)> GetBroadcastAdapters()
    {
        var result = new List<(IPAddress, IPEndPoint)>();
        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up) continue;
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(unicast.Address)) continue;
                    if (unicast.IPv4Mask == null) continue;

                    var ip = unicast.Address;
                    var firstByte = ip.GetAddressBytes()[0];
                    if (firstByte == 169) continue;

                    var broadcast = GetBroadcastAddress(ip, unicast.IPv4Mask);
                    if (broadcast.Equals(ip)) continue;

                    result.Add((ip, new IPEndPoint(broadcast, NetworkConstants.DiscoveryPort)));
                }
            }
        }
        catch (Exception ex)
        {
            NetDiagnostics.Log($"GetBroadcastAdapters error: {ex.Message}");
        }

        return result.Distinct().ToList();
    }

    private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
    {
        var ipBytes = address.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var broadcast = new byte[4];
        for (var i = 0; i < 4; i++)
            broadcast[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
        return new IPAddress(broadcast);
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var ctx = await _listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleRequest(ctx), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath?.Trim('/') ?? "";
            var method = ctx.Request.HttpMethod;
            var remoteIp = ctx.Request.RemoteEndPoint?.Address?.ToString() ?? "?";
            NetDiagnostics.Log($"→ {method} /{path} from {remoteIp}");

            if (method == "GET" && path == "api/ping")
            {
                await ReplyJson(ctx, new { status = "ok", server = Environment.MachineName, timestamp = DateTime.UtcNow });
                return;
            }

            if (method == "GET" && path == "api/clients")
            {
                await ReplyJson(ctx, Clients);
                return;
            }

            if (method == "GET" && path == "api/reports")
            {
                await ReplyJson(ctx, Reports);
                return;
            }

            if (method == "GET" && path.StartsWith("api/reports/") && path.EndsWith("/pdf"))
            {
                var id = path.Split('/')[2];
                var filePath = Directory.GetFiles(ReportsDirectory, $"{id}_*.pdf").FirstOrDefault();
                if (filePath != null)
                {
                    ctx.Response.ContentType = "application/pdf";
                    ctx.Response.StatusCode = 200;
                    await using var fs = File.OpenRead(filePath);
                    await fs.CopyToAsync(ctx.Response.OutputStream);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    await ReplyJson(ctx, new { error = "Relatório não encontrado" });
                }
                ctx.Response.Close();
                return;
            }

            if (method == "GET" && path.StartsWith("api/reports/"))
            {
                var id = path.Split('/')[2];
                var report = Reports.FirstOrDefault(r => r.Id == id);
                if (report != null)
                    await ReplyJson(ctx, report);
                else
                {
                    ctx.Response.StatusCode = 404;
                    await ReplyJson(ctx, new { error = "Relatório não encontrado" });
                }
                return;
            }

            if (method == "POST" && path == "api/client/heartbeat")
            {
                using var reader = new StreamReader(ctx.Request.InputStream);
                var body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                var id = data.GetProperty("id").GetString() ?? "";
                var hostName = data.GetProperty("hostName").GetString() ?? "";
                var machineIp = ctx.Request.RemoteEndPoint?.Address?.ToString() ?? "";
                var machineId = id;
                NetDiagnostics.Log($"Heartbeat from {hostName} ({machineIp})");

                lock (_lock)
                {
                    var existing = _clients.FirstOrDefault(c => c.Id == machineId);
                    if (existing != null)
                    {
                        _clients.Remove(existing);
                        _clients.Add(existing with { LastSeen = DateTime.UtcNow });
                    }
                    else
                    {
                        _clients.Add(new RemoteMachineInfo(machineId, hostName, machineIp, Port, DateTime.UtcNow, DateTime.UtcNow));
                    }
                    CleanupStaleClients();
                }

                ClientsChanged?.Invoke(this, EventArgs.Empty);
                await ReplyJson(ctx, new { status = "ok" });
                NetDiagnostics.Log($"Heartbeat OK from {hostName}");
                return;
            }

            if (method == "POST" && path == "api/reports")
            {
                var machineId = ctx.Request.QueryString["machineId"] ?? "unknown";
                var machineName = ctx.Request.QueryString["machineName"] ?? "unknown";
                var testType = ctx.Request.QueryString["testType"] ?? "Desconhecido";
                var duration = ctx.Request.QueryString["duration"] ?? "00:00:00";
                var status = ctx.Request.QueryString["status"] ?? "Desconhecido";
                var result = ctx.Request.QueryString["result"] ?? "Pendente";
                var contentLength = ctx.Request.ContentLength64;
                NetDiagnostics.Log($"Receiving report: machine={machineName}, type={testType}, contentLength={contentLength}");

                var reportId = $"{machineId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                var fileName = $"{reportId}_{machineName}_{testType}.pdf";
                var filePath = Path.Combine(ReportsDirectory, fileName);

                await using (var fs = File.Create(filePath))
                {
                    await ctx.Request.InputStream.CopyToAsync(fs);
                }

                var fileInfo = new FileInfo(filePath);
                NetDiagnostics.Log($"Saved {fileInfo.Length} bytes to {fileName}");

                lock (_lock)
                {
                    _reports.Add(new RemoteReportInfo(
                        reportId, machineId, machineName, testType,
                        DateTime.UtcNow, duration, status, result, fileInfo.Length));
                }

                PersistReports();
                ReportsChanged?.Invoke(this, EventArgs.Empty);
                await ReplyJson(ctx, new { id = reportId, status = "received" });
                NetDiagnostics.Log($"Report {reportId} registered, reporting {Reports.Count} reports now");
                return;
            }

            NetDiagnostics.Log($"404 — {method} /{path}");
            ctx.Response.StatusCode = 404;
            await ReplyJson(ctx, new { error = "Endpoint não encontrado" });
        }
        catch (Exception ex)
        {
            NetDiagnostics.Log($"Error handling request: {ex.GetType().Name}: {ex.Message}");
            try
            {
                ctx.Response.StatusCode = 500;
                await ReplyJson(ctx, new { error = ex.Message });
            }
            catch { }
        }
        finally
        {
            try { ctx.Response.Close(); } catch { }
        }
    }

    private static Task ReplyJson(HttpListenerContext ctx, object data)
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentLength64 = bytes.Length;
        return ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    private void CleanupStaleClients()
    {
        var cutoff = DateTime.UtcNow.AddMilliseconds(-NetworkConstants.ServerTimeoutMs * 2);
        _clients.RemoveAll(c => c.LastSeen < cutoff);
    }

    public void Dispose()
    {
        Stop();
        _listener?.Close();
    }

    private static void EnsureFirewallRule()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netsh",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            psi.Arguments = $"advfirewall firewall show rule name=\"EME Diagnostics Server\"";
            using var check = System.Diagnostics.Process.Start(psi);
            if (check == null) return;
            check.WaitForExit(2000);
            if (check.StandardOutput.ReadToEnd().Contains("EME Diagnostics Server"))
            {
                NetDiagnostics.Log("Firewall rules already exist");
                return;
            }

            NetDiagnostics.Log("Creating firewall rules...");
            var add = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"EME Diagnostics Server\" dir=in action=allow protocol=TCP localport={NetworkConstants.ServerPort} profile=private,public",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            add?.WaitForExit(2000);

            var addUdp = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"EME Diagnostics Discovery\" dir=in action=allow protocol=UDP localport={NetworkConstants.DiscoveryPort} profile=private,public",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            addUdp?.WaitForExit(2000);
            NetDiagnostics.Log("Firewall rules created");
        }
        catch (Exception ex)
        {
            NetDiagnostics.Log($"Failed to configure firewall: {ex.Message}");
        }
    }
}
