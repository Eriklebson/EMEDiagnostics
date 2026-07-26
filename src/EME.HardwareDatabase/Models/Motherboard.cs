namespace EME.HardwareDatabase.Models;

public sealed class Motherboard
{
    public string Id { get; set; } = string.Empty;
    public string ManufacturerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SuperIoId { get; set; }
    public string? Chipset { get; set; }
    public string? FormFactor { get; set; }
}
