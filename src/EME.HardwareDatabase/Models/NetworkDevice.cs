namespace EME.HardwareDatabase.Models;

public sealed class NetworkDevice
{
    public string Id { get; set; } = string.Empty;
    public string ManufacturerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Interface { get; set; }
    public string? WirelessStandard { get; set; }
    public string DeviceType { get; set; } = "Wired";
}
