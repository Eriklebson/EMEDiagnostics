namespace EME.HardwareDatabase.Models;

public sealed class CpuSensorMapping
{
    public int Id { get; set; }
    public string? CpuModelId { get; set; }
    public string? CpuArchitectureId { get; set; }
    public string SensorType { get; set; } = string.Empty;
    public string PreferredName { get; set; } = string.Empty;
    public int Priority { get; set; }
}
