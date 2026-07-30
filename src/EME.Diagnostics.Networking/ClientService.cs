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

                while (!ct.IsCancellationRequested)
                {
                    var result = await listener.ReceiveAsync(ct);
                    var message = Encoding.UTF8.GetString(result.Buffer);
                    var parts = message.Split('|');

                    if (parts.Length >= 3 && parts[0] == NetworkConstants.AdvertisementMessage)
                    {
                        var hostName = parts[1];
                        var port = int.Parse(parts[2]);
                        var ip = result.RemoteEndPoint.Address.ToString();

                        var server = new ServerInfo(hostName, ip, port);

                        if (_connectedServer == null || _connectedServer.HostName != hostName)
                        {
                            _connectedServer = server;
                            _http.BaseAddress = new Uri($"http://{ip}:{port}/");
                            ServerConnected?.Invoke(this, server);

                            _ = Task.Run(() => HeartbeatLoop(ct), ct);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException)
            {
                await Task.Delay(3000, ct);
            }
        }
    }

    private async Task HeartbeatLoop(CancellationToken ct)
    {
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

                    if (!response.IsSuccessStatusCode)
                    {
                        OnServerLost();
                        return;
                    }

                    await Task.Delay(NetworkConstants.HeartbeatIntervalMs, ct);
                }
                catch (HttpRequestException)
                {
                    OnServerLost();
                    return;
                }
                catch (OperationCanceledException) { return; }
            }
        }
        catch { OnServerLost(); }
    }

    public async Task<bool> SendReportAsync(string pdfPath, string testType, string duration, string status, string result)
    {
        if (_connectedServer == null || _http.BaseAddress == null)
            return false;

        try
        {
            var url = $"api/reports?machineId={Uri.EscapeDataString(MachineId)}&machineName={Uri.EscapeDataString(MachineName)}&testType={Uri.EscapeDataString(testType)}&duration={Uri.EscapeDataString(duration)}&status={Uri.EscapeDataString(status)}&result={Uri.EscapeDataString(result)}";

            await using var fs = File.OpenRead(pdfPath);
            var content = new StreamContent(fs);
            var response = await _http.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
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
