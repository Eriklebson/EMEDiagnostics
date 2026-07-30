namespace EME.Diagnostics.Networking.Models;

public sealed record RemoteMachineInfo(
    string Id,
    string HostName,
    string IpAddress,
    int Port,
    DateTime FirstSeen,
    DateTime LastSeen);
