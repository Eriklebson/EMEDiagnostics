namespace EME.HardwareDatabase.Detection;

public sealed class StorageDetectionIdentity
{
    public string Name { get; set; } = string.Empty;
    public string? Vendor { get; set; }
    public string? Interface { get; set; }
    public ulong? CapacityBytes { get; set; }
    public string? Firmware { get; set; }
}
