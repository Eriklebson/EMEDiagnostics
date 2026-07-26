namespace EME.HardwareDatabase.Models;

public sealed class CpuModel
{
    public string Id { get; set; } = string.Empty;
    public string FamilyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? Cores { get; set; }
    public int? Threads { get; set; }
    public double? BaseClock { get; set; }
    public double? BoostClock { get; set; }
    public int? Tdp { get; set; }
}
