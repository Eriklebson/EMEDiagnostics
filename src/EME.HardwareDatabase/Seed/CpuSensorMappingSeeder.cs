using System.Diagnostics;
using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Services;

namespace EME.HardwareDatabase.Seed;

public sealed class CpuSensorMappingSeeder : ISeedImporter
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public string SourceName => "CPU Sensor Mappings";

    public CpuSensorMappingSeeder(DatabaseConnectionFactory connectionFactory)
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
            clear.CommandText = "DELETE FROM CpuSensorMappings";
            clear.ExecuteNonQuery();

            var architectures = GetArchitectures(conn);
            Debug.WriteLine($"  Gerando sensor mappings para {architectures.Count} arquiteturas CPU");

            foreach (var (archId, manId) in architectures)
            {
                if (ct.IsCancellationRequested) break;
                imported += InsertSensorMappings(conn, archId, manId);
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            errors.Add($"Erro no CpuSensorMappingSeeder: {ex.Message}");
        }

        return Task.FromResult(new SeedResult(SourceName, imported, errors.Count,
            errors.Count > 0 ? errors.AsReadOnly() : null));
    }

    private static List<(string ArchId, string ManId)> GetArchitectures(SqliteConnection conn)
    {
        var list = new List<(string, string)>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ManufacturerId FROM CpuArchitectures";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetString(0), reader.GetString(1)));
        return list;
    }

    private static int InsertSensorMappings(SqliteConnection conn, string archId, string manId)
    {
        var count = 0;
        var isAmd = manId.Equals("amd", StringComparison.OrdinalIgnoreCase);

        var mappings = isAmd ? AmdMappings : IntelMappings;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO CpuSensorMappings (CpuModelId, CpuArchitectureId, SensorType, PreferredName, Priority)
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

    private static readonly (string Type, string Pref)[] AmdMappings =
    [
        ("Temperature", "Core (Tctl/Tdie)"),
        ("Temperature", "Tctl"),
        ("Temperature", "Tdie"),
        ("Temperature", "CCD #1"),
        ("Temperature", "CCD #2"),
        ("Temperature", "CPU Package"),
        ("Temperature", "Core #0"),
        ("Temperature", "Core #1"),
        ("Temperature", "Core #2"),
        ("Temperature", "Core #3"),
        ("Temperature", "Core #4"),
        ("Temperature", "Core #5"),
        ("Temperature", "Core #6"),
        ("Temperature", "Core #7"),
        ("Temperature", "Core #8"),
        ("Temperature", "Core #9"),
        ("Temperature", "Core #10"),
        ("Temperature", "Core #11"),
        ("Temperature", "Core #12"),
        ("Temperature", "Core #13"),
        ("Temperature", "Core #14"),
        ("Temperature", "Core #15"),
        ("Temperature", "Core #16"),
        ("Temperature", "Core #17"),
        ("Temperature", "Core #18"),
        ("Temperature", "Core #19"),
        ("Temperature", "Core #20"),
        ("Temperature", "Core #21"),
        ("Temperature", "Core #22"),
        ("Temperature", "Core #23"),
        ("Load", "CPU Total"),
        ("Load", "Core #0"),
        ("Load", "Core #1"),
        ("Load", "Core #2"),
        ("Load", "Core #3"),
        ("Load", "Core #4"),
        ("Load", "Core #5"),
        ("Load", "Core #6"),
        ("Load", "Core #7"),
        ("Load", "Core #8"),
        ("Load", "Core #9"),
        ("Load", "Core #10"),
        ("Load", "Core #11"),
        ("Load", "Core #12"),
        ("Load", "Core #13"),
        ("Load", "Core #14"),
        ("Load", "Core #15"),
        ("Load", "Core #16"),
        ("Load", "Core #17"),
        ("Load", "Core #18"),
        ("Load", "Core #19"),
        ("Load", "Core #20"),
        ("Load", "Core #21"),
        ("Load", "Core #22"),
        ("Load", "Core #23"),
        ("Power", "CPU Package"),
        ("Power", "Core (SMU)"),
        ("Fan", "CPU Fan"),
        ("Clock", "Core #0"),
        ("Clock", "Core #1"),
        ("Clock", "Core #2"),
        ("Clock", "Core #3"),
        ("Voltage", "CPU Core"),
        ("Voltage", "CPU VCore"),
        ("Voltage", "CPU SOC"),
        ("Voltage", "CPU +12V"),
        ("Voltage", "CPU +5V"),
        ("Voltage", "CPU +3.3V"),
    ];

    private static readonly (string Type, string Pref)[] IntelMappings =
    [
        ("Temperature", "CPU Package"),
        ("Temperature", "Core #0"),
        ("Temperature", "Core #1"),
        ("Temperature", "Core #2"),
        ("Temperature", "Core #3"),
        ("Temperature", "Core #4"),
        ("Temperature", "Core #5"),
        ("Temperature", "Core #6"),
        ("Temperature", "Core #7"),
        ("Temperature", "Core #8"),
        ("Temperature", "Core #9"),
        ("Temperature", "Core #10"),
        ("Temperature", "Core #11"),
        ("Temperature", "Core #12"),
        ("Temperature", "Core #13"),
        ("Temperature", "Core #14"),
        ("Temperature", "Core #15"),
        ("Temperature", "Core Average"),
        ("Temperature", "Core Max"),
        ("Load", "CPU Total"),
        ("Load", "Core #0"),
        ("Load", "Core #1"),
        ("Load", "Core #2"),
        ("Load", "Core #3"),
        ("Load", "Core #4"),
        ("Load", "Core #5"),
        ("Load", "Core #6"),
        ("Load", "Core #7"),
        ("Load", "Core #8"),
        ("Load", "Core #9"),
        ("Load", "Core #10"),
        ("Load", "Core #11"),
        ("Load", "Core #12"),
        ("Load", "Core #13"),
        ("Load", "Core #14"),
        ("Load", "Core #15"),
        ("Load", "Core #16"),
        ("Load", "Core #17"),
        ("Load", "Core #18"),
        ("Load", "Core #19"),
        ("Load", "Core #20"),
        ("Load", "Core #21"),
        ("Load", "Core #22"),
        ("Load", "Core #23"),
        ("Load", "Core (P-Cores)"),
        ("Load", "Core (E-Cores)"),
        ("Power", "CPU Package"),
        ("Fan", "CPU Fan"),
        ("Clock", "Core #0"),
        ("Clock", "Core #1"),
        ("Clock", "Core #2"),
        ("Clock", "Core #3"),
        ("Clock", "Core #4"),
        ("Clock", "Core #5"),
        ("Clock", "Core #6"),
        ("Clock", "Core #7"),
        ("Voltage", "CPU Core"),
        ("Voltage", "CPU VCore"),
        ("Voltage", "CPU +12V"),
        ("Voltage", "CPU +5V"),
        ("Voltage", "CPU +3.3V"),
    ];
}
