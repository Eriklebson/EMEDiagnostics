namespace EME.HardwareDatabase.Models;

public sealed class DatabaseMetadata
{
    public int Id { get; set; }
    public string SchemaVersion { get; set; } = "1.0.0";
    public string DataVersion { get; set; } = "2026.07.001";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string MinimumCoreVersion { get; set; } = "2.26.0";
    public string MinimumDiagnosticsVersion { get; set; } = "1.0.0";
    public string? Checksum { get; set; }
}
