namespace EME.HardwareDatabase.Models;

public sealed class MemoryModel
{
    public string Id { get; set; } = string.Empty;
    public string StandardId { get; set; } = string.Empty;
    public string ManufacturerId { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public int? CapacityMb { get; set; }
    public int? SpeedMhz { get; set; }
    public string? FormFactor { get; set; }
    public bool Ecc { get; set; }
    public bool Registered { get; set; }
}
