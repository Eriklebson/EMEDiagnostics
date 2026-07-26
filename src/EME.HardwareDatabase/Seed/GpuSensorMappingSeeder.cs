using System.Diagnostics;
using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Services;

namespace EME.HardwareDatabase.Seed;

public sealed class GpuSensorMappingSeeder : ISeedImporter
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public string SourceName => "GPU Sensor Mappings";

    public GpuSensorMappingSeeder(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<SeedResult> ImportAsync(CancellationToken ct = default)
    {
        var imported = 0;
        var errors = new List<string>();

        try
        {
            using var conn = _connectionFactory.CreateConnection();
            using var tx = conn.BeginTransaction();

            // Remove mappings existentes para garantir idempotência
            using var clear = conn.CreateCommand();
            clear.CommandText = "DELETE FROM GpuSensorMappings";
            clear.ExecuteNonQuery();

            var architectures = GetArchitectures(conn);
            Debug.WriteLine($"  Gerando sensor mappings para {architectures.Count} arquiteturas GPU");

            foreach (var (archId, manId) in architectures)
            {
                if (ct.IsCancellationRequested) break;
                imported += InsertSensorMappings(conn, archId, manId);
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            errors.Add($"Erro no GpuSensorMappingSeeder: {ex.Message}");
        }

        return Task.FromResult(new SeedResult(SourceName, imported, errors.Count,
            errors.Count > 0 ? errors.AsReadOnly() : null));
    }

    private static List<(string ArchId, string ManId)> GetArchitectures(SqliteConnection conn)
    {
        var list = new List<(string, string)>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ManufacturerId FROM GpuArchitectures";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetString(0), reader.GetString(1)));
        return list;
    }

    private static int InsertSensorMappings(SqliteConnection conn, string archId, string manId)
    {
        var count = 0;

        var mappings = manId.ToLowerInvariant() switch
        {
            "nvidia" => NvidiaMappings,
            "amd" => AmdMappings,
            "intel" => IntelMappings,
            _ => GenericMappings,
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO GpuSensorMappings (GpuModelId, GpuArchitectureId, SensorType, PreferredName, Priority)
            VALUES (NULL, $aid, $type, $pref, $pri)
            """;

        var priority = 0;
        foreach (var (type, pref) in mappings)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$aid", archId);
            cmd.Parameters.AddWithValue("$type", type);
            cmd.Parameters.AddWithValue("$pref", pref);
            cmd.Parameters.AddWithValue("$pri", priority++);
            cmd.ExecuteNonQuery();
            count++;
        }

        return count;
    }

    private static readonly (string Type, string Pref)[] NvidiaMappings =
    [
        ("Temperature", "GPU Core"),
        ("Temperature", "GPU Memory"),
        ("Temperature", "GPU Hot Spot"),
        ("Temperature", "GPU VRAM"),
        ("Load", "GPU Core"),
        ("Load", "GPU Memory"),
        ("Load", "GPU Video Engine"),
        ("Load", "GPU Bus"),
        ("Fan", "GPU Fan"),
        ("Power", "GPU Package"),
        ("Power", "GPU Total Board"),
        ("Clock", "GPU Core"),
        ("Clock", "GPU Memory"),
        ("Clock", "GPU Shader"),
        ("Voltage", "GPU Core"),
        ("Voltage", "GPU +12V"),
        ("Voltage", "GPU +3.3V"),
    ];

    private static readonly (string Type, string Pref)[] AmdMappings =
    [
        ("Temperature", "GPU Core"),
        ("Temperature", "GPU Memory"),
        ("Temperature", "GPU Hot Spot"),
        ("Temperature", "GPU VRM"),
        ("Temperature", "GPU VRM Core"),
        ("Load", "GPU Core"),
        ("Load", "GPU Memory"),
        ("Load", "GPU Video Engine"),
        ("Load", "GPU Bus"),
        ("Fan", "GPU Fan"),
        ("Power", "GPU Package"),
        ("Power", "GPU PPT"),
        ("Clock", "GPU Core"),
        ("Clock", "GPU Memory"),
        ("Clock", "GPU SOC"),
        ("Voltage", "GPU Core"),
        ("Voltage", "GPU SOC"),
        ("Voltage", "GPU Memory"),
    ];

    private static readonly (string Type, string Pref)[] IntelMappings =
    [
        ("Temperature", "GPU Core"),
        ("Temperature", "GPU Package"),
        ("Load", "GPU Core"),
        ("Load", "GPU Render"),
        ("Fan", "GPU Fan"),
        ("Power", "GPU Package"),
        ("Clock", "GPU Core"),
        ("Clock", "GPU Memory"),
        ("Voltage", "GPU Core"),
    ];

    private static readonly (string Type, string Pref)[] GenericMappings =
    [
        ("Temperature", "GPU Core"),
        ("Load", "GPU Core"),
        ("Fan", "GPU Fan"),
        ("Power", "GPU Package"),
        ("Clock", "GPU Core"),
        ("Voltage", "GPU Core"),
    ];
}
