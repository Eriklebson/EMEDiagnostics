namespace EME.HardwareDatabase.Models;

public sealed class HardwareAlias
{
    public int Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string MatchMode { get; set; } = "Substring";
}
