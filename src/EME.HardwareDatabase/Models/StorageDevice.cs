namespace EME.HardwareDatabase.Models;

public sealed class StorageDevice
{
    public string Id { get; set; } = string.Empty;
    public string? ControllerId { get; set; }
    public string ManufacturerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FormFactor { get; set; }
    public int? CapacityGb { get; set; }
}
