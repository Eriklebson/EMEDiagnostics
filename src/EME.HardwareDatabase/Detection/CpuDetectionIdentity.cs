namespace EME.HardwareDatabase.Detection;

public sealed class CpuDetectionIdentity
{
    public string Name { get; set; } = string.Empty;
    public string? Vendor { get; set; }
    public string? Architecture { get; set; }
    public string? Family { get; set; }
    public int? PhysicalCores { get; set; }
    public int? LogicalCores { get; set; }
    public double? BaseClock { get; set; }
}
