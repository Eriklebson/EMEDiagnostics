namespace EME.HardwareDatabase.Models;

public sealed class GpuModel
{
    public string Id { get; set; } = string.Empty;
    public string ArchitectureId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? VramMb { get; set; }
    public string? VramType { get; set; }
}
