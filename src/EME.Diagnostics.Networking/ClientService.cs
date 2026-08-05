using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EME.Diagnostics.Networking.Models;

namespace EME.Diagnostics.Networking;

public sealed class ClientService : IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly HttpClient _http = new();
    private ServerInfo? _connectedServer;

    public bool IsConnected => _connectedServer != null;

    public ServerInfo? ConnectedServer => _connectedServer;

    public event EventHandler<ServerInfo>? ServerConnected;
    public event EventHandler? ServerDisconnected;

    public string MachineId { get; }
    public string MachineName { get; }

    public ClientService()
    {
        MachineName = Environment.MachineName;
        MachineId = $"{MachineName}_{Environment.UserName}";
    }

    public Task StartDiscoveryAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        NetDiagnostics.Log($"Starting discovery (UDP port {NetworkConstants.DiscoveryPort})...");
        _ = Task.Run(() => DiscoveryLoop(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    private async Task DiscoveryLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var listener = new UdpClient(NetworkConstants.DiscoveryPort);
                listener.EnableBroadcast = true;
                NetDiagnostics.Log($"Listening for UDP broadcasts on port {NetworkConstants.DiscoveryPort}...");

                while (!ct.IsCancellationRequested)
                {
                    var result = await listener.ReceiveAsync(ct);
                    var message = Encoding.UTF8.GetString(result.Buffer);
                    var parts = message.Split('|');
                    NetDiagnostics.Log($"Received UDP: {message}");

                    if (parts.Length >= 3 && parts[0] == NetworkConstants.AdvertisementMessage)
                    {
                        var hostName = parts[1];
                        var port = int.Parse(parts[2]);
                        var ip = result.RemoteEndPoint.Address.ToString();

                        var server = new ServerInfo(hostName, ip, port);
                        NetDiagnostics.Log($"Discovered server: {hostName} @ {ip}:{port}");

                        if (_connectedServer == null || _connectedServer.HostName != hostName)
                        {
                            _connectedServer = server;
                            _http.BaseAddress = new Uri($"http://{ip}:{port}/");
                            NetDiagnostics.Log($"Connected to {hostName}, baseAddress={_http.BaseAddress}");
                            ServerConnected?.Invoke(this, server);

                            _ = Task.Run(() => HeartbeatLoop(ct), ct);
                        }
                        else
                        {
                            NetDiagnostics.Log($"Already connected to {hostName}, ignoring");
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException ex)
            {
                NetDiagnostics.Log($"SocketException: {ex.Message}, retrying in 3s...");
                await Task.Delay(3000, ct);
            }
        }
    }

    private async Task HeartbeatLoop(CancellationToken ct)
    {
        NetDiagnostics.Log($"HeartbeatLoop started (interval={NetworkConstants.HeartbeatIntervalMs}ms)");
        try
        {
            while (!ct.IsCancellationRequested && _connectedServer != null)
            {
                try
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        id = MachineId,
                        hostName = MachineName
                    });
                    var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    var response = await _http.PostAsync("api/client/heartbeat", content, ct);
                    NetDiagnostics.Log($"Heartbeat response: {(int)response.StatusCode}");

                    if (!response.IsSuccessStatusCode)
                    {
                        NetDiagnostics.Log($"Heartbeat failed with status {(int)response.StatusCode}, disconnecting");
                        OnServerLost();
                        return;
                    }

                    await Task.Delay(NetworkConstants.HeartbeatIntervalMs, ct);
                }
                catch (HttpRequestException ex)
                {
                    NetDiagnostics.Log($"Heartbeat HttpRequestException: {ex.Message}, disconnecting");
                    OnServerLost();
                    return;
                }
                catch (OperationCanceledException)
                {
                    NetDiagnostics.Log("Heartbeat cancelled");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            NetDiagnostics.Log($"Heartbeat unexpected error: {ex.Message}");
            OnServerLost();
        }
        NetDiagnostics.Log("HeartbeatLoop ended");
    }

    public async Task<bool> SendReportAsync(string pdfPath, string testType, string duration, string status, string result)
    {
        NetDiagnostics.Log($"SendReportAsync called: pdfPath={pdfPath}, testType={testType}, connected={_connectedServer != null}, baseAddress={_http.BaseAddress}");

        if (_connectedServer == null || _http.BaseAddress == null)
        {
            NetDiagnostics.Log($"Cannot send: connectedServer={_connectedServer != null}, baseAddress={_http.BaseAddress != null}");
            return false;
        }

        try
        {
            var url = $"api/reports?machineId={Uri.EscapeDataString(MachineId)}&machineName={Uri.EscapeDataString(MachineName)}&testType={Uri.EscapeDataString(testType)}&duration={Uri.EscapeDataString(duration)}&status={Uri.EscapeDataString(status)}&result={Uri.EscapeDataString(result)}";
            NetDiagnostics.Log($"Full URL: {_http.BaseAddress}{url}");

            var bytes = await File.ReadAllBytesAsync(pdfPath);
            NetDiagnostics.Log($"Read {bytes.Length} bytes from {pdfPath}");

            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            NetDiagnostics.Log($"Posting to {url}...");

            var response = await _http.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();
            NetDiagnostics.Log($"Response: {(int)response.StatusCode} {response.StatusCode} — {body}");

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            NetDiagnostics.Log($"Exception: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PingServerAsync(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await client.GetAsync($"{url.TrimEnd('/')}/api/ping");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private void OnServerLost()
    {
        _connectedServer = null;
        ServerDisconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        Stop();
        _http.Dispose();
    }
}

public sealed record ServerInfo(string HostName, string IpAddress, int Port);
