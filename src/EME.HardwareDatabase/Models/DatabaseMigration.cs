namespace EME.HardwareDatabase.Models;

public sealed class DatabaseMigration
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
    public string? Checksum { get; set; }
    public bool Success { get; set; }
}
