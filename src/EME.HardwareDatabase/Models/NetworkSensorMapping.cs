namespace EME.HardwareDatabase.Models;

public sealed class NetworkSensorMapping
{
    public int Id { get; set; }
    public string? NetworkDeviceId { get; set; }
    public string SensorType { get; set; } = string.Empty;
    public string PreferredName { get; set; } = string.Empty;
    public int Priority { get; set; }
}
