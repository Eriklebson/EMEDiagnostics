using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EME.Diagnostics.Networking.Models;

namespace EME.Diagnostics.Networking;

public sealed class ServerService : IDisposable
{
    private HttpListener? _listener;
    private UdpClient? _udpAdvertiser;
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

    public ServerService()
    {
        ReportsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EMEDiagnostics", "network_reports");
        Directory.CreateDirectory(ReportsDirectory);
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{Port}/");
        _listener.Start();

        _ = Task.Run(() => AdvertiseLoop(_cts.Token), _cts.Token);
        _ = Task.Run(() => AcceptLoop(_cts.Token), _cts.Token);

        await Task.CompletedTask;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _udpAdvertiser?.Close();
    }

    private async Task AdvertiseLoop(CancellationToken ct)
    {
        _udpAdvertiser = new UdpClient();
        var endpoint = new IPEndPoint(IPAddress.Broadcast, NetworkConstants.DiscoveryPort);
        var hostName = Environment.MachineName;
        var message = $"{NetworkConstants.AdvertisementMessage}|{hostName}|{Port}";
        var bytes = Encoding.UTF8.GetBytes(message);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _udpAdvertiser.SendAsync(bytes, bytes.Length, endpoint);
                await Task.Delay(NetworkConstants.HeartbeatIntervalMs, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { break; }
        }
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
                var remoteIp = ctx.Request.RemoteEndPoint?.Address?.ToString() ?? "";
                var machineId = id;

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
                        _clients.Add(new RemoteMachineInfo(machineId, hostName, remoteIp, Port, DateTime.UtcNow, DateTime.UtcNow));
                    }
                    CleanupStaleClients();
                }

                ClientsChanged?.Invoke(this, EventArgs.Empty);
                await ReplyJson(ctx, new { status = "ok" });
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
                var reportId = $"{machineId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                var fileName = $"{reportId}_{machineName}_{testType}.pdf";
                var filePath = Path.Combine(ReportsDirectory, fileName);

                await using (var fs = File.Create(filePath))
                {
                    await ctx.Request.InputStream.CopyToAsync(fs);
                }

                var fileInfo = new FileInfo(filePath);
                lock (_lock)
                {
                    _reports.Add(new RemoteReportInfo(
                        reportId, machineId, machineName, testType,
                        DateTime.UtcNow, duration, status, result, fileInfo.Length));
                }

                ReportsChanged?.Invoke(this, EventArgs.Empty);
                await ReplyJson(ctx, new { id = reportId, status = "received" });
                return;
            }

            ctx.Response.StatusCode = 404;
            await ReplyJson(ctx, new { error = "Endpoint não encontrado" });
        }
        catch (Exception ex)
        {
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
        _udpAdvertiser?.Close();
    }
}
