namespace EME.HardwareDatabase.Models;

public sealed class PsuSensorMapping
{
    public int Id { get; set; }
    public string? PowerSupplyId { get; set; }
    public string SensorType { get; set; } = string.Empty;
    public string PreferredName { get; set; } = string.Empty;
    public int Priority { get; set; }
}
