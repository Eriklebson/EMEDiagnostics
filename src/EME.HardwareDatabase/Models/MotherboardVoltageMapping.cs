namespace EME.HardwareDatabase.Models;

public sealed class MotherboardVoltageMapping
{
    public int Id { get; set; }
    public string MotherboardId { get; set; } = string.Empty;
    public string RawName { get; set; } = string.Empty;
    public string MappedName { get; set; } = string.Empty;
}
