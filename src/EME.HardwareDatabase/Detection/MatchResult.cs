namespace EME.HardwareDatabase.Detection;

public sealed class MatchResult
{
    public string? ProfileId { get; set; }
    public MatchLevel MatchLevel { get; set; } = MatchLevel.Unknown;
    public int MatchConfidence { get; set; }
    public string? DisplayName { get; set; }
}

public enum MatchLevel
{
    Exact = 0,
    Architecture = 1,
    Family = 2,
    Alias = 3,
    Generic = 4,
    Unknown = 5
}
