namespace EME.HardwareDatabase.Models;

public sealed class GpuSensorMapping
{
    public int Id { get; set; }
    public string? GpuModelId { get; set; }
    public string? GpuArchitectureId { get; set; }
    public string SensorType { get; set; } = string.Empty;
    public string PreferredName { get; set; } = string.Empty;
    public int Priority { get; set; }
}
