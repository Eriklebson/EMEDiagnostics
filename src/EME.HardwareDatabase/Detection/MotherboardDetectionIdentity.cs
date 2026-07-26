namespace EME.HardwareDatabase.Detection;

public sealed class MotherboardDetectionIdentity
{
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? BiosVersion { get; set; }
    public string? SuperIoName { get; set; }
}
