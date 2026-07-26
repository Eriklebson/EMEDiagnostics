using System.Text.Json;
using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Services;

namespace EME.HardwareDatabase.Import;

public sealed class LegacyHardwareMappingImporter
{
    private readonly DatabaseConnectionFactory _connectionFactory;
    private const string CpuJsonPath = @"config\cpu-sensors-mapping.json";
    private const string MoboJsonPath = @"config\hardware-mapping.json";

    public LegacyHardwareMappingImporter(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void ImportAll()
    {
        ImportCpuMapping();
        ImportMotherboardMapping();
    }

    public void ImportCpuMapping()
    {
        if (!File.Exists(CpuJsonPath)) return;
        var json = File.ReadAllText(CpuJsonPath);
        var doc = JsonSerializer.Deserialize<CpuMappingJson>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (doc?.Architectures == null) return;

        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();

        foreach (var (archId, arch) in doc.Architectures)
        {
            if (arch.Match == null || arch.Sensors == null) continue;
            var vendor = arch.Vendor ?? "Unknown";
            var vendorId = vendor.ToLowerInvariant();

            EnsureManufacturer(conn, vendor);
            var archDbId = $"{vendorId}.cpu.architecture.{archId.ToLowerInvariant()}";
            var segment = InferSegment(arch.Match);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT OR IGNORE INTO CpuArchitectures " +
                                  "(Id, ManufacturerId, Name, DisplayName, Segment) VALUES ($id, $mid, $name, $disp, $seg)";
                cmd.Parameters.AddWithValue("$id", archDbId);
                cmd.Parameters.AddWithValue("$mid", vendorId);
                cmd.Parameters.AddWithValue("$name", archId);
                cmd.Parameters.AddWithValue("$disp", $"{vendor} {archId}");
                cmd.Parameters.AddWithValue("$seg", segment);
                cmd.ExecuteNonQuery();
            }

            InsertSensorMapping(conn, null, archDbId, "Temperature", arch.Sensors.TempSensor, 0);
            InsertSensorMapping(conn, null, archDbId, "Power", arch.Sensors.PowerSensor, 0);

            if (arch.Sensors.TempFallback != null)
            {
                for (int i = 0; i < arch.Sensors.TempFallback.Count; i++)
                    InsertSensorMapping(conn, null, archDbId, "Temperature", arch.Sensors.TempFallback[i], i + 1);
            }

            foreach (var alias in arch.Match)
            {
                using var acmd = conn.CreateCommand();
                acmd.CommandText = "INSERT OR IGNORE INTO HardwareAliases (TargetType, TargetId, Alias, MatchMode) " +
                                   "VALUES ($tt, $tid, $a, 'Substring')";
                acmd.Parameters.AddWithValue("$tt", "CpuArchitecture");
                acmd.Parameters.AddWithValue("$tid", archDbId);
                acmd.Parameters.AddWithValue("$a", alias);
                acmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    public void ImportMotherboardMapping()
    {
        if (!File.Exists(MoboJsonPath)) return;
        var json = File.ReadAllText(MoboJsonPath);
        var doc = JsonSerializer.Deserialize<MotherboardMappingJson>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (doc?.Motherboards == null) return;

        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();

        foreach (var mb in doc.Motherboards)
        {
            var moboId = NormalizeId(mb.Name);
            var vendorId = mb.Name.StartsWith("ASUS", StringComparison.OrdinalIgnoreCase) ? "asus"
                : mb.Name.StartsWith("GIGABYTE", StringComparison.OrdinalIgnoreCase) ? "gigabyte"
                : mb.Name.StartsWith("MSI", StringComparison.OrdinalIgnoreCase) ? "msi"
                : "unknown";

            EnsureManufacturer(conn, vendorId);
            EnsureSuperIo(conn, mb.Chip);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT OR IGNORE INTO Motherboards (Id, ManufacturerId, Name, SuperIoId) " +
                                  "VALUES ($id, $mid, $name, $sid)";
                cmd.Parameters.AddWithValue("$id", moboId);
                cmd.Parameters.AddWithValue("$mid", vendorId);
                cmd.Parameters.AddWithValue("$name", mb.Name);
                cmd.Parameters.AddWithValue("$sid", mb.Chip != null ? (object)NormalizeId(mb.Chip) : DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            if (mb.FanMapping != null)
            {
                foreach (var (raw, mapped) in mb.FanMapping)
                {
                    using var fcmd = conn.CreateCommand();
                    fcmd.CommandText = "INSERT INTO MotherboardFanMappings (MotherboardId, RawName, MappedName, Category) " +
                                       "VALUES ($mid, $raw, $mapped, 'Motherboard')";
                    fcmd.Parameters.AddWithValue("$mid", moboId);
                    fcmd.Parameters.AddWithValue("$raw", raw);
                    fcmd.Parameters.AddWithValue("$mapped", mapped);
                    fcmd.ExecuteNonQuery();
                }
            }

            if (mb.TemperatureMapping != null)
            {
                foreach (var (raw, mapped) in mb.TemperatureMapping)
                {
                    using var tcmd = conn.CreateCommand();
                    tcmd.CommandText = "INSERT INTO MotherboardTemperatureMappings (MotherboardId, RawName, MappedName) " +
                                       "VALUES ($mid, $raw, $mapped)";
                    tcmd.Parameters.AddWithValue("$mid", moboId);
                    tcmd.Parameters.AddWithValue("$raw", raw);
                    tcmd.Parameters.AddWithValue("$mapped", mapped);
                    tcmd.ExecuteNonQuery();
                }
            }

            if (mb.VoltageMapping != null)
            {
                foreach (var (raw, mapped) in mb.VoltageMapping)
                {
                    using var vcmd = conn.CreateCommand();
                    vcmd.CommandText = "INSERT INTO MotherboardVoltageMappings (MotherboardId, RawName, MappedName) " +
                                       "VALUES ($mid, $raw, $mapped)";
                    vcmd.Parameters.AddWithValue("$mid", moboId);
                    vcmd.Parameters.AddWithValue("$raw", raw);
                    vcmd.Parameters.AddWithValue("$mapped", mapped);
                    vcmd.ExecuteNonQuery();
                }
            }
        }

        tx.Commit();
    }

    private static void EnsureManufacturer(SqliteConnection conn, string id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO Manufacturers (Id, DisplayName, ShortName) VALUES ($id, $name, $name)";
        cmd.Parameters.AddWithValue("$id", id.ToLowerInvariant());
        cmd.Parameters.AddWithValue("$name", char.ToUpperInvariant(id[0]) + id[1..]);
        cmd.ExecuteNonQuery();
    }

    private static void EnsureSuperIo(SqliteConnection conn, string? chip)
    {
        if (string.IsNullOrEmpty(chip)) return;
        var id = NormalizeId(chip);
        var vendorId = chip.StartsWith("Nuvoton", StringComparison.OrdinalIgnoreCase) ? "nuvoton"
            : chip.StartsWith("ITE", StringComparison.OrdinalIgnoreCase) ? "ite"
            : chip.StartsWith("Fintek", StringComparison.OrdinalIgnoreCase) ? "fintek"
            : "unknown";

        EnsureManufacturer(conn, vendorId);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO SuperIoChips (Id, ManufacturerId, Name) VALUES ($id, $mid, $name)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$mid", vendorId);
        cmd.Parameters.AddWithValue("$name", chip);
        cmd.ExecuteNonQuery();
    }

    private static void InsertSensorMapping(SqliteConnection conn, string? modelId, string? archId,
        string sensorType, string? name, int priority)
    {
        if (string.IsNullOrEmpty(name)) return;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO CpuSensorMappings (CpuModelId, CpuArchitectureId, SensorType, PreferredName, Priority) " +
                          "VALUES ($mid, $aid, $st, $pn, $pri)";
        cmd.Parameters.AddWithValue("$mid", (object?)modelId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$aid", (object?)archId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$st", sensorType);
        cmd.Parameters.AddWithValue("$pn", name);
        cmd.Parameters.AddWithValue("$pri", priority);
        cmd.ExecuteNonQuery();
    }

    private static string NormalizeId(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("/", "-")
            .Replace("_", "-")
            .Replace(".", "-")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("--", "-")
            .Trim('-');
    }

    private static string InferSegment(List<string> patterns)
    {
        var all = string.Join(" ", patterns);
        if (all.Contains("G", StringComparison.OrdinalIgnoreCase) &&
            (all.Contains("U", StringComparison.OrdinalIgnoreCase) ||
             all.Contains("HS", StringComparison.OrdinalIgnoreCase) ||
             all.Contains("HX", StringComparison.OrdinalIgnoreCase)))
            return "Mobile";
        if (all.Contains("Threadripper", StringComparison.OrdinalIgnoreCase))
            return "HEDT";
        if (all.Contains("EPYC", StringComparison.OrdinalIgnoreCase) ||
            all.Contains("Xeon", StringComparison.OrdinalIgnoreCase))
            return "Server";
        if (all.Contains("Ultra", StringComparison.OrdinalIgnoreCase) ||
            all.Contains("i5-", StringComparison.OrdinalIgnoreCase) ||
            all.Contains("i7-", StringComparison.OrdinalIgnoreCase))
            return "Desktop";
        if (all.Contains("G", StringComparison.OrdinalIgnoreCase))
            return "Desktop_APU";
        return "Desktop";
    }
}

internal sealed class CpuMappingJson
{
    public Dictionary<string, ArchitectureJson>? Architectures { get; set; }
}

internal sealed class ArchitectureJson
{
    public List<string>? Match { get; set; }
    public string? Vendor { get; set; }
    public CpuSensorSetJson? Sensors { get; set; }
}

internal sealed class CpuSensorSetJson
{
    public string? TempSensor { get; set; }
    public string? PowerSensor { get; set; }
    public List<string>? TempFallback { get; set; }
}

internal sealed class MotherboardMappingJson
{
    public List<MotherboardEntryJson>? Motherboards { get; set; }
}

internal sealed class MotherboardEntryJson
{
    public string Name { get; set; } = string.Empty;
    public string? Chip { get; set; }
    public Dictionary<string, string>? FanMapping { get; set; }
    public Dictionary<string, string>? TemperatureMapping { get; set; }
    public Dictionary<string, string>? VoltageMapping { get; set; }
}
