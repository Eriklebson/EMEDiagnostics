namespace EME.HardwareDatabase.Detection;

public sealed class MemoryDetectionIdentity
{
    public string? Manufacturer { get; set; }
    public string? PartNumber { get; set; }
    public string? FormFactor { get; set; }
    public int? CapacityMb { get; set; }
    public int? SpeedMhz { get; set; }
    public string? MemoryType { get; set; }
}
