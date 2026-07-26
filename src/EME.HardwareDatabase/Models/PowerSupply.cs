namespace EME.HardwareDatabase.Models;

public sealed class PowerSupply
{
    public string Id { get; set; } = string.Empty;
    public string ManufacturerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "ATX";
    public int? Wattage { get; set; }
    public string? Efficiency { get; set; }
    public string? Modular { get; set; }
}
