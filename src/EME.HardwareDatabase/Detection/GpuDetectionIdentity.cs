namespace EME.HardwareDatabase.Detection;

public sealed class GpuDetectionIdentity
{
    public string Name { get; set; } = string.Empty;
    public string? Vendor { get; set; }
    public string? Architecture { get; set; }
    public ulong? VramBytes { get; set; }
}
