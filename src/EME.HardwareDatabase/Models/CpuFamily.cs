namespace EME.HardwareDatabase.Models;

public sealed class CpuFamily
{
    public string Id { get; set; } = string.Empty;
    public string ArchitectureId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
