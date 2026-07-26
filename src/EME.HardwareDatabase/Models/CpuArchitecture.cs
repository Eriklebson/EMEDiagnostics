namespace EME.HardwareDatabase.Models;

public sealed class CpuArchitecture
{
    public string Id { get; set; } = string.Empty;
    public string ManufacturerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public int? Released { get; set; }
}
