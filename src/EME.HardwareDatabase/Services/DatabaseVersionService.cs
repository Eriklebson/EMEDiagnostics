using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Models;
using EME.HardwareDatabase.Shared;

namespace EME.HardwareDatabase.Services;

public sealed class DatabaseVersionService
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public DatabaseVersionService(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public DatabaseMetadata? ReadMetadata()
    {
        try
        {
            using var conn = _connectionFactory.CreateReadOnlyConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, SchemaVersion, DataVersion, CreatedAt, UpdatedAt, " +
                              "MinimumCoreVersion, MinimumDiagnosticsVersion, Checksum FROM DatabaseMetadata LIMIT 1";
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new DatabaseMetadata
            {
                Id = reader.GetInt32(0),
                SchemaVersion = reader.GetString(1),
                DataVersion = reader.GetString(2),
                CreatedAt = reader.GetDateTime(3),
                UpdatedAt = reader.GetDateTime(4),
                MinimumCoreVersion = reader.IsDBNull(5) ? "0.0.0" : reader.GetString(5),
                MinimumDiagnosticsVersion = reader.IsDBNull(6) ? "0.0.0" : reader.GetString(6),
                Checksum = reader.IsDBNull(7) ? null : reader.GetString(7)
            };
        }
        catch { return null; }
    }

    public bool NeedsSchemaUpdate(string currentSchemaVersion)
    {
        var meta = ReadMetadata();
        if (meta == null) return true;
        return CompareVersions(meta.SchemaVersion, currentSchemaVersion) < 0;
    }

    public bool NeedsDataUpdate(string currentDataVersion)
    {
        var meta = ReadMetadata();
        if (meta == null) return true;
        return CompareVersions(meta.DataVersion, currentDataVersion) < 0;
    }

    public bool IsDowngrade(string newVersion, bool isSchema)
    {
        var meta = ReadMetadata();
        if (meta == null) return false;
        var current = isSchema ? meta.SchemaVersion : meta.DataVersion;
        return CompareVersions(current, newVersion) > 0;
    }

    public void UpdateMetadata(string schemaVersion, string dataVersion)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE DatabaseMetadata SET SchemaVersion = $sv, DataVersion = $dv, " +
                          "UpdatedAt = $now, MinimumCoreVersion = $mcv, MinimumDiagnosticsVersion = $mdv WHERE Id = 1";
        cmd.Parameters.AddWithValue("$sv", schemaVersion);
        cmd.Parameters.AddWithValue("$dv", dataVersion);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("$mcv", Constants.MinimumCoreVersion);
        cmd.Parameters.AddWithValue("$mdv", Constants.MinimumDiagnosticsVersion);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public bool IsSchemaCompatible(int maxSupportedSchema)
    {
        var meta = ReadMetadata();
        if (meta == null) return true;
        var parts = meta.SchemaVersion.Split('.');
        if (parts.Length == 0) return true;
        return int.TryParse(parts[0], out var major) && major <= maxSupportedSchema;
    }

    private static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var pb = b.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var va = i < pa.Length ? pa[i] : 0;
            var vb = i < pb.Length ? pb[i] : 0;
            if (va != vb) return va.CompareTo(vb);
        }
        return 0;
    }
}
