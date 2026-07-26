using System.Diagnostics;
using System.Data;
using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Services;

namespace EME.HardwareDatabase.Seed;

public sealed class MotherboardMappingSeeder : ISeedImporter
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public string SourceName => "Motherboard Sensor Mappings";

    public MotherboardMappingSeeder(DatabaseConnectionFactory connectionFactory)
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

            var motherboards = GetMotherboards(conn);
            Debug.WriteLine($"  Gerando mappings para {motherboards.Count} motherboards");

            foreach (var (moboId, superIoId) in motherboards)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrEmpty(superIoId)) continue;

                var template = GetTemplate(superIoId);
                if (template == null) continue;

                // Remove mappings antigos, insere novos baseados no chip
                DeleteExistingMappings(conn, moboId);
                imported += InsertMappings(conn, moboId, template);
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            errors.Add($"Erro no MotherboardMappingSeeder: {ex.Message}");
        }

        return Task.FromResult(new SeedResult(SourceName, imported, errors.Count,
            errors.Count > 0 ? errors.AsReadOnly() : null));
    }

    private static List<(string Id, string? SuperIo)> GetMotherboards(SqliteConnection conn)
    {
        var list = new List<(string, string?)>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, SuperIoId FROM Motherboards";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
        return list;
    }

    private static void DeleteExistingMappings(SqliteConnection conn, string moboId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM MotherboardFanMappings WHERE MotherboardId = $id";
        cmd.Parameters.AddWithValue("$id", moboId);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "DELETE FROM MotherboardTemperatureMappings WHERE MotherboardId = $id";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "DELETE FROM MotherboardVoltageMappings WHERE MotherboardId = $id";
        cmd.ExecuteNonQuery();
    }

    private static int InsertMappings(SqliteConnection conn, string moboId, SuperIoTemplate template)
    {
        var count = 0;

        using var fanCmd = conn.CreateCommand();
        fanCmd.CommandText = """
            INSERT INTO MotherboardFanMappings (MotherboardId, RawName, MappedName, Category)
            VALUES ($mid, $raw, $map, $cat)
            """;

        foreach (var (raw, mapped, cat) in template.Fans)
        {
            fanCmd.Parameters.Clear();
            fanCmd.Parameters.AddWithValue("$mid", moboId);
            fanCmd.Parameters.AddWithValue("$raw", raw);
            fanCmd.Parameters.AddWithValue("$map", mapped);
            fanCmd.Parameters.AddWithValue("$cat", cat);
            fanCmd.ExecuteNonQuery();
            count++;
        }

        using var tempCmd = conn.CreateCommand();
        tempCmd.CommandText = """
            INSERT INTO MotherboardTemperatureMappings (MotherboardId, RawName, MappedName)
            VALUES ($mid, $raw, $map)
            """;

        foreach (var (raw, mapped) in template.Temps)
        {
            tempCmd.Parameters.Clear();
            tempCmd.Parameters.AddWithValue("$mid", moboId);
            tempCmd.Parameters.AddWithValue("$raw", raw);
            tempCmd.Parameters.AddWithValue("$map", mapped);
            tempCmd.ExecuteNonQuery();
            count++;
        }

        using var voltCmd = conn.CreateCommand();
        voltCmd.CommandText = """
            INSERT INTO MotherboardVoltageMappings (MotherboardId, RawName, MappedName)
            VALUES ($mid, $raw, $map)
            """;

        foreach (var (raw, mapped) in template.Volts)
        {
            voltCmd.Parameters.Clear();
            voltCmd.Parameters.AddWithValue("$mid", moboId);
            voltCmd.Parameters.AddWithValue("$raw", raw);
            voltCmd.Parameters.AddWithValue("$map", mapped);
            voltCmd.ExecuteNonQuery();
            count++;
        }

        return count;
    }

    private static SuperIoTemplate? GetTemplate(string superIoId)
    {
        var key = superIoId.ToLowerInvariant();

        if (key.Contains("nct6798d")) return Nct6798D;
        if (key.Contains("nct6797d")) return Nct6797D;
        if (key.Contains("nct6796d")) return Nct6796D;
        if (key.Contains("nct6795d")) return Nct6795D;
        if (key.Contains("nct6793d")) return Nct6793D;
        if (key.Contains("nct6792d")) return Nct6792D;
        if (key.Contains("nct6791d")) return Nct6791D;
        if (key.Contains("nct6687")) return Nct6687;
        if (key.Contains("nct6686")) return Nct6686;
        if (key.Contains("nct5585")) return Nct5585;
        if (key.Contains("nct5572")) return Nct5572;
        if (key.Contains("nct5104")) return Nct5104;
        if (key.Contains("it8792e")) return It8792E;
        if (key.Contains("it8786e")) return It8786E;
        if (key.Contains("it8783e")) return It8783E;
        if (key.Contains("it8781e")) return It8781E;
        if (key.Contains("it8728f")) return It8728F;
        if (key.Contains("it8725f")) return It8725F;
        if (key.Contains("it8686e")) return It8686E;
        if (key.Contains("it8688e")) return It8688E;
        if (key.Contains("it8692e")) return It8692E;
        if (key.Contains("it8613e")) return It8613E;
        if (key.Contains("it8625e")) return It8625E;
        if (key.Contains("it8620e")) return It8620E;
        if (key.Contains("it8655e")) return It8655E;
        if (key.Contains("f71878ad")) return F71878Ad;
        if (key.Contains("f71869ed")) return F71869Ed;
        if (key.Contains("f71808e")) return F71808E;
        if (key.Contains("f71858")) return F71858;
        if (key.Contains("f71882")) return F71882;
        if (key.Contains("f71862")) return F71862;
        if (key.Contains("f81768")) return F81768;
        if (key.Contains("emc6d103")) return Emc6D103;
        if (key.Contains("w83627")) return W83627;
        if (key.Contains("w83667")) return W83667;
        if (key.Contains("w83677")) return W83677;

        return key.Contains("nuvoton") ? NctGeneric : null;
    }

    private sealed record SuperIoTemplate(
        (string Raw, string Mapped, string Category)[] Fans,
        (string Raw, string Mapped)[] Temps,
        (string Raw, string Mapped)[] Volts
    );

    // ── Nuvoton NCT6798D (ASUS ROG Z790/X670, MSI MEG/MPG Z790/X670E) ──────────
    private static readonly SuperIoTemplate Nct6798D = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("CPU Opt Fan", "CPU Optional Fan", "Motherboard"),
        ("Chassis Fan #1", "Chassis Fan #1", "Motherboard"),
        ("Chassis Fan #2", "Chassis Fan #2", "Motherboard"),
        ("Chassis Fan #3", "Chassis Fan #3", "Motherboard"),
        ("Chassis Fan #4", "Chassis Fan #4", "Motherboard"),
        ("AIO Pump", "AIO Pump", "Motherboard"),
        ("Water Pump", "Water Pump", "Motherboard"),
        ("Water Flow", "Water Flow", "Motherboard"),
        ("VRM Fan", "VRM Fan", "Motherboard"),
    ],
    [
        ("SYSTIN", "Motherboard Temperature"),
        ("CPUTIN", "CPU Temperature"),
        ("AUXTIN", "Auxiliary Temperature"),
        ("TMPIN4", "PCH Temperature"),
        ("TMPIN5", "VRM Temperature"),
        ("TMPIN6", "MOS Temperature"),
    ],
    [
        ("VIN0", "CPU VCore"),
        ("VIN1", "+12V"),
        ("VIN2", "+5V"),
        ("VIN3", "+3.3V"),
        ("VIN4", "CPU VDD"),
        ("VIN5", "DRAM Voltage"),
        ("VIN6", "PCH Voltage"),
        ("VIN7", "VBAT"),
    ]);

    // ── Nuvoton NCT6797D ────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate Nct6797D = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("CPU Opt Fan", "CPU Optional Fan", "Motherboard"),
        ("Chassis Fan #1", "Chassis Fan #1", "Motherboard"),
        ("Chassis Fan #2", "Chassis Fan #2", "Motherboard"),
        ("Chassis Fan #3", "Chassis Fan #3", "Motherboard"),
        ("Water Pump", "Water Pump", "Motherboard"),
    ],
    [
        ("SYSTIN", "Motherboard Temperature"),
        ("CPUTIN", "CPU Temperature"),
        ("AUXTIN", "Auxiliary Temperature"),
        ("TMPIN4", "PCH Temperature"),
    ],
    [
        ("VIN0", "CPU VCore"),
        ("VIN1", "+12V"),
        ("VIN2", "+5V"),
        ("VIN3", "+3.3V"),
        ("VIN4", "CPU VDD"),
        ("VIN5", "DRAM Voltage"),
    ]);

    // ── Nuvoton NCT6796D (ASUS PRIME, MSI PRO, ASRock) ──────────────────────────
    private static readonly SuperIoTemplate Nct6796D = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("CPU Opt Fan", "CPU Optional Fan", "Motherboard"),
        ("Chassis Fan #1", "Chassis Fan #1", "Motherboard"),
        ("Chassis Fan #2", "Chassis Fan #2", "Motherboard"),
        ("Chassis Fan #3", "Chassis Fan #3", "Motherboard"),
        ("Pump Fan", "Pump Fan", "Motherboard"),
    ],
    [
        ("SYSTIN", "Motherboard Temperature"),
        ("CPUTIN", "CPU Temperature"),
        ("AUXTIN", "Auxiliary Temperature"),
    ],
    [
        ("VIN0", "CPU VCore"),
        ("VIN1", "+12V"),
        ("VIN2", "+5V"),
        ("VIN3", "+3.3V"),
        ("VIN4", "CPU VDD"),
    ]);

    // ── Nuvoton NCT6795D (ASUS X570/B550, MSI X570/B550, EVGA) ──────────────────
    private static readonly SuperIoTemplate Nct6795D = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("CPU Opt Fan", "CPU Optional Fan", "Motherboard"),
        ("Chassis Fan #1", "Chassis Fan #1", "Motherboard"),
        ("Chassis Fan #2", "Chassis Fan #2", "Motherboard"),
        ("Chassis Fan #3", "Chassis Fan #3", "Motherboard"),
        ("Pump Fan", "Pump Fan", "Motherboard"),
        ("VRM Fan", "VRM Fan", "Motherboard"),
    ],
    [
        ("SYSTIN", "Motherboard Temperature"),
        ("CPUTIN", "CPU Temperature"),
        ("AUXTIN", "Auxiliary Temperature"),
        ("TMPIN4", "PCH Temperature"),
    ],
    [
        ("VIN0", "CPU VCore"),
        ("VIN1", "+12V"),
        ("VIN2", "+5V"),
        ("VIN3", "+3.3V"),
        ("VIN4", "CPU VDD"),
        ("VIN5", "DRAM Voltage"),
    ]);

    // ── Nuvoton NCT6793D (ASUS Z390/Z370, MSI B450/Z390, ASRock B450) ──────────
    private static readonly SuperIoTemplate Nct6793D = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("Chassis Fan #1", "Chassis Fan #1", "Motherboard"),
        ("Chassis Fan #2", "Chassis Fan #2", "Motherboard"),
        ("Chassis Fan #3", "Chassis Fan #3", "Motherboard"),
        ("Pump Fan", "Pump Fan", "Motherboard"),
    ],
    [
        ("SYSTIN", "Motherboard Temperature"),
        ("CPUTIN", "CPU Temperature"),
        ("AUXTIN", "Auxiliary Temperature"),
    ],
    [
        ("VIN0", "CPU VCore"),
        ("VIN1", "+12V"),
        ("VIN2", "+5V"),
        ("VIN3", "+3.3V"),
    ]);

    // ── Nuvoton NCT6792D ────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate Nct6792D = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("Chassis Fan #1", "Chassis Fan #1", "Motherboard"),
        ("Chassis Fan #2", "Chassis Fan #2", "Motherboard"),
        ("Chassis Fan #3", "Chassis Fan #3", "Motherboard"),
    ],
    [
        ("SYSTIN", "Motherboard Temperature"),
        ("CPUTIN", "CPU Temperature"),
        ("AUXTIN", "Auxiliary Temperature"),
    ],
    [
        ("VIN0", "CPU VCore"),
        ("VIN1", "+12V"),
        ("VIN2", "+5V"),
        ("VIN3", "+3.3V"),
    ]);

    // ── Nuvoton NCT6791D ────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate Nct6791D = Nct6792D;

    // ── Nuvoton NCT6687 (some ASUS ROG) ─────────────────────────────────────────
    private static readonly SuperIoTemplate Nct6687 = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("CPU Opt Fan", "CPU Optional Fan", "Motherboard"),
        ("Chassis Fan #1", "Chassis Fan #1", "Motherboard"),
        ("Chassis Fan #2", "Chassis Fan #2", "Motherboard"),
        ("Chassis Fan #3", "Chassis Fan #3", "Motherboard"),
        ("Water Pump", "Water Pump", "Motherboard"),
        ("Water Flow", "Water Flow", "Motherboard"),
    ],
    [
        ("System", "Motherboard Temperature"),
        ("CPU", "CPU Temperature"),
        ("PCH", "PCH Temperature"),
        ("VRM", "VRM Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
    ]);

    private static readonly SuperIoTemplate Nct6686 = Nct6687;

    // ── Nuvoton NCT5585D ────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate Nct5585 = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("Chassis Fan #1", "Chassis Fan #1", "Motherboard"),
        ("Chassis Fan #2", "Chassis Fan #2", "Motherboard"),
        ("Chassis Fan #3", "Chassis Fan #3", "Motherboard"),
    ],
    [
        ("SYSTIN", "Motherboard Temperature"),
        ("CPUTIN", "CPU Temperature"),
    ],
    [
        ("VIN0", "CPU VCore"),
        ("VIN1", "+12V"),
        ("VIN2", "+5V"),
        ("VIN3", "+3.3V"),
    ]);

    // ── Nuvoton NCT5572D ────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate Nct5572 = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("Chassis Fan #1", "Chassis Fan #1", "Motherboard"),
        ("Chassis Fan #2", "Chassis Fan #2", "Motherboard"),
    ],
    [
        ("SYSTIN", "Motherboard Temperature"),
        ("CPUTIN", "CPU Temperature"),
    ],
    [
        ("VIN0", "CPU VCore"),
        ("VIN1", "+12V"),
        ("VIN2", "+5V"),
        ("VIN3", "+3.3V"),
    ]);

    // ── Nuvoton NCT5104 (Supermicro) ────────────────────────────────────────────
    private static readonly SuperIoTemplate Nct5104 = new(
    [
        ("CPU Fan #1", "CPU Fan #1", "Motherboard"),
        ("CPU Fan #2", "CPU Fan #2", "Motherboard"),
        ("System Fan #1", "System Fan #1", "Motherboard"),
        ("System Fan #2", "System Fan #2", "Motherboard"),
        ("System Fan #3", "System Fan #3", "Motherboard"),
        ("System Fan #4", "System Fan #4", "Motherboard"),
        ("System Fan #5", "System Fan #5", "Motherboard"),
        ("System Fan #6", "System Fan #6", "Motherboard"),
        ("System Fan #7", "System Fan #7", "Motherboard"),
        ("System Fan #8", "System Fan #8", "Motherboard"),
    ],
    [
        ("CPU", "CPU Temperature"),
        ("System", "System Temperature"),
        ("Peripheral", "Peripheral Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
        ("VBAT", "Battery Voltage"),
    ]);

    // ── Genérico Nuvoton ────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate NctGeneric = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("Chassis Fan #1", "Chassis Fan #1", "Motherboard"),
        ("Chassis Fan #2", "Chassis Fan #2", "Motherboard"),
        ("Chassis Fan #3", "Chassis Fan #3", "Motherboard"),
    ],
    [
        ("SYSTIN", "Motherboard Temperature"),
        ("CPUTIN", "CPU Temperature"),
    ],
    [
        ("VIN0", "CPU VCore"),
        ("VIN1", "+12V"),
        ("VIN2", "+5V"),
        ("VIN3", "+3.3V"),
    ]);

    // ── ITE IT8688E (Gigabyte AORUS Z790/X670/B760/B650/X570/B550) ──────────────
    private static readonly SuperIoTemplate It8688E = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("System Fan #1", "System Fan #1", "Motherboard"),
        ("System Fan #2", "System Fan #2", "Motherboard"),
        ("System Fan #3", "System Fan #3", "Motherboard"),
        ("System Fan #4", "System Fan #4", "Motherboard"),
        ("System Fan #5", "System Fan #5", "Motherboard"),
        ("Pump Fan", "Pump Fan", "Motherboard"),
    ],
    [
        ("System", "Motherboard Temperature"),
        ("CPU", "CPU Temperature"),
        ("VRM", "VRM Temperature"),
        ("PCH", "PCH Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
        ("DRAM", "DRAM Voltage"),
        ("PCH", "PCH Voltage"),
    ]);

    // ── ITE IT8686E (Gigabyte B450/Z390) ────────────────────────────────────────
    private static readonly SuperIoTemplate It8686E = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("System Fan #1", "System Fan #1", "Motherboard"),
        ("System Fan #2", "System Fan #2", "Motherboard"),
        ("System Fan #3", "System Fan #3", "Motherboard"),
        ("Pump Fan", "Pump Fan", "Motherboard"),
    ],
    [
        ("System", "Motherboard Temperature"),
        ("CPU", "CPU Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
    ]);

    // ── ITE IT8792E ─────────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate It8792E = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("System Fan #1", "System Fan #1", "Motherboard"),
        ("System Fan #2", "System Fan #2", "Motherboard"),
        ("System Fan #3", "System Fan #3", "Motherboard"),
    ],
    [
        ("System", "Motherboard Temperature"),
        ("CPU", "CPU Temperature"),
        ("VRM", "VRM Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
    ]);

    // ── ITE IT8786E ─────────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate It8786E = It8792E;

    // ── ITE IT8783E ─────────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate It8783E = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("System Fan #1", "System Fan #1", "Motherboard"),
        ("System Fan #2", "System Fan #2", "Motherboard"),
    ],
    [
        ("System", "Motherboard Temperature"),
        ("CPU", "CPU Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
    ]);

    private static readonly SuperIoTemplate It8781E = It8783E;

    // ── ITE IT8728F ─────────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate It8728F = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("System Fan #1", "System Fan #1", "Motherboard"),
        ("System Fan #2", "System Fan #2", "Motherboard"),
    ],
    [
        ("System", "Motherboard Temperature"),
        ("CPU", "CPU Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
    ]);

    private static readonly SuperIoTemplate It8725F = It8728F;

    // ── ITE IT8692E ─────────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate It8692E = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("System Fan #1", "System Fan #1", "Motherboard"),
        ("System Fan #2", "System Fan #2", "Motherboard"),
        ("System Fan #3", "System Fan #3", "Motherboard"),
    ],
    [
        ("System", "Motherboard Temperature"),
        ("CPU", "CPU Temperature"),
        ("PCH", "PCH Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
    ]);

    // ── ITE IT8613E ─────────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate It8613E = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("System Fan #1", "System Fan #1", "Motherboard"),
    ],
    [
        ("System", "Motherboard Temperature"),
        ("CPU", "CPU Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
    ]);

    // ── ITE IT8625E / IT8620E ─────────────────────────────────────────────────
    private static readonly SuperIoTemplate It8625E = It8686E;
    private static readonly SuperIoTemplate It8620E = It8686E;
    private static readonly SuperIoTemplate It8655E = It8686E;

    // ── Fintek F71878AD ─────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate F71878Ad = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("System Fan #1", "System Fan #1", "Motherboard"),
        ("System Fan #2", "System Fan #2", "Motherboard"),
        ("System Fan #3", "System Fan #3", "Motherboard"),
    ],
    [
        ("System", "Motherboard Temperature"),
        ("CPU", "CPU Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
    ]);

    private static readonly SuperIoTemplate F71869Ed = F71878Ad;
    private static readonly SuperIoTemplate F71808E = F71878Ad;
    private static readonly SuperIoTemplate F71858 = F71878Ad;
    private static readonly SuperIoTemplate F71882 = F71878Ad;
    private static readonly SuperIoTemplate F71862 = F71878Ad;
    private static readonly SuperIoTemplate F81768 = F71878Ad;

    // ── Microchip EMC6D103 ──────────────────────────────────────────────────────
    private static readonly SuperIoTemplate Emc6D103 = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("System Fan #1", "System Fan #1", "Motherboard"),
    ],
    [
        ("System", "Motherboard Temperature"),
        ("CPU", "CPU Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
    ]);

    // ── Winbond W83627HF ────────────────────────────────────────────────────────
    private static readonly SuperIoTemplate W83627 = new(
    [
        ("CPU Fan", "CPU Fan", "Motherboard"),
        ("System Fan #1", "System Fan #1", "Motherboard"),
        ("System Fan #2", "System Fan #2", "Motherboard"),
    ],
    [
        ("System", "Motherboard Temperature"),
        ("CPU", "CPU Temperature"),
    ],
    [
        ("VCore", "CPU VCore"),
        ("VCC3.3V", "+3.3V"),
        ("VCC5V", "+5V"),
        ("VCC12V", "+12V"),
    ]);

    private static readonly SuperIoTemplate W83667 = W83627;
    private static readonly SuperIoTemplate W83677 = W83627;
}
