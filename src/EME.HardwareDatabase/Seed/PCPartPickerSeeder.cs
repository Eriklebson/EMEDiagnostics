using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Services;

namespace EME.HardwareDatabase.Seed;

public sealed class PCPartPickerSeeder : ISeedImporter
{
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly HttpClient _http;

    private const string BaseUrl = "https://raw.githubusercontent.com/docyx/pc-part-dataset/main/data/json";

    public string SourceName => "PCPartPicker Dataset";

    public PCPartPickerSeeder(DatabaseConnectionFactory connectionFactory, HttpClient? http = null)
    {
        _connectionFactory = connectionFactory;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    public async Task<SeedResult> ImportAsync(CancellationToken ct = default)
    {
        var total = 0;
        var errors = new List<string>();

        try
        {
            using var conn = _connectionFactory.CreateConnection();
            using var tx = conn.BeginTransaction();

            EnsureManufacturers(conn);

            total += await ImportMotherboards(conn, ct);
            total += await ImportMemory(conn, ct);
            total += await ImportStorage(conn, ct);
            total += await ImportPowerSupplies(conn, ct);
            total += await ImportNetworkDevices(conn, ct);
            total += SeedPsuSensorMappings(conn, ct);
            total += SeedNetworkSensorMappings(conn, ct);

            tx.Commit();
        }
        catch (Exception ex)
        {
            errors.Add($"Erro no PCPartPickerSeeder: {ex.Message}");
        }

        return new SeedResult(SourceName, total, errors.Count,
            errors.Count > 0 ? errors.AsReadOnly() : null);
    }

    // ── Manufacturers ──────────────────────────────────────────────────────

    private static void EnsureManufacturers(SqliteConnection conn)
    {
        var manufacturers = new (string Id, string DisplayName)[]
        {
            ("thermaltake", "Thermaltake"),
            ("coolermaster", "Cooler Master"),
            ("noctua", "Noctua"),
            ("bequiet", "be quiet!"),
            ("arctic", "Arctic"),
            ("nzxt", "NZXT"),
            ("phanteks", "Phanteks"),
            ("lianli", "Lian Li"),
            ("fractal", "Fractal Design"),
            ("corsair", "Corsair Memory"),
            ("seasonic", "Seasonic"),
            ("evga", "EVGA Corporation"),
            ("coolermaster", "Cooler Master"),
            ("adata", "ADATA Technology"),
            ("teamgroup", "TEAMGROUP"),
            ("patriot", "Patriot Memory"),
            ("pny", "PNY Technologies"),
            ("mushkin", "Mushkin Enhanced"),
            ("siliconpower", "Silicon Power"),
            ("geil", "GeIL"),
            ("ocz", "OCZ Technology"),
            ("tp-link", "TP-Link"),
            ("asus", "ASUSTeK Computer"),
            ("asrock", "ASRock"),
            ("gigabyte", "Gigabyte Technology"),
            ("msi", "Micro-Star International"),
            ("biostar", "Biostar"),
            ("colorful", "Colorful Technology"),
            ("maxsun", "MAXSUN"),
            ("superflower", "Super Flower"),
            ("fsp", "FSP Group"),
            ("antec", "Antec"),
            ("rosewill", "Rosewill"),
            ("xpg", "XPG"),
            ("samsung", "Samsung"),
            ("skhynix", "SK Hynix"),
            ("micron", "Micron Technology"),
            ("crucial", "Crucial"),
            ("kingston", "Kingston Technology"),
            ("gskill", "G.Skill"),
            ("westerndigital", "Western Digital"),
            ("seagate", "Seagate Technology"),
            ("toshiba", "Toshiba"),
            ("sandisk", "SanDisk"),
            ("intel", "Intel"),
            ("amd", "AMD"),
            ("nvidia", "NVIDIA"),
            ("hp", "HP Inc"),
            ("dell", "Dell"),
            ("lenovo", "Lenovo"),
            ("acer", "Acer"),
            ("sony", "Sony"),
            ("lg", "LG Electronics"),
            ("samsung", "Samsung"),
            ("foxconn", "Foxconn"),
            ("superflower", "Super Flower"),
            ("enermax", "Enermax"),
            ("silverstone", "SilverStone Technology"),
            ("deepcool", "DeepCool"),
            ("ekwb", "EKWB"),
            ("alphacool", "Alphacool"),
            ("raijintek", "Raijintek"),
            ("scythe", "Scythe"),
            ("zalman", "Zalman"),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO Manufacturers (Id, DisplayName, ShortName) VALUES ($id, $dn, $sn)";

        foreach (var (id, name) in manufacturers)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$dn", name);
            cmd.Parameters.AddWithValue("$sn", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ── Motherboards (4,973 items) ─────────────────────────────────────────

    private async Task<int> ImportMotherboards(SqliteConnection conn, CancellationToken ct)
    {
        var items = await FetchJsonAsync<PCPartMotherboard>("motherboard", ct);
        if (items == null) return 0;

        Debug.WriteLine($"  Motherboards: {items.Count} baixados");

        var count = 0;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO Motherboards (Id, ManufacturerId, Name, Chipset, FormFactor)
            VALUES ($id, $mid, $name, $chip, $ff)
            """;

        foreach (var mobo in items)
        {
            if (ct.IsCancellationRequested) break;

            var manu = ExtractManufacturer(mobo.Name ?? "", conn);
            var chipset = ExtractChipset(mobo.Name ?? "");
            var ff = NormalizeFormFactor(mobo.FormFactor ?? "");
            var id = $"{manu.Id}.mobo.{NormalizeId(mobo.Name ?? "")}";

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$mid", manu.Id);
            cmd.Parameters.AddWithValue("$name", mobo.Name ?? "");
            cmd.Parameters.AddWithValue("$chip", (object?)chipset ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ff", (object?)ff ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            count++;
        }

        Debug.WriteLine($"  Motherboards: {count} importados");
        return count;
    }

    private static string? ExtractChipset(string name)
    {
        // Common chipset patterns in motherboard names
        var patterns = new[] {
            @"\b(X870E|X870|X670E|X670|B850|B840|B650E|B650|A620)\b",
            @"\b(Z790|Z690|Z590|Z490|Z390|Z370|Z270|Z170|Z97|Z87|Z77|Z68|Z75)\b",
            @"\b(H810|H770|H670|H610|H570|H510|H470|H410|H370|H310|H270|H170|H110)\b",
            @"\b(B760|B660|B560|B460|B365|B360|B250|B150|B85|B75)\b",
            @"\b(Q670|Q570|Q470|Q370|Q270|Q170)\b",
            @"\b(W790|W680|W580|W480|W280|C621|C622|C602|C612|C422|C246)\b",
            @"\b(X299|X399|X99|X79|X58|X38|X48)\b",
            @"\b(TRX50|TRX40|WRX90|WRX80)\b",
            @"\b(A520|A320|A88X|A78|A68|A58|A55|A75|A85X)\b",
            @"\b(AM5|AM4|AM3|AM3\+|AM2|AM2\+|FM2|FM2\+|FM1)\b",
            @"\b(LGA1851|LGA1700|LGA1200|LGA1151|LGA1150|LGA1155|LGA1156|LGA2066|LGA2011|LGA3647|LGA4677|LGA7529)\b",
            @"\b(sWRX8|sTRX4|sTR5|SP3|SP5|SP6|SP7)\b",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(name, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Value.ToUpperInvariant();
        }

        return null;
    }

    // ── Memory (13,553 items) ──────────────────────────────────────────────

    private async Task<int> ImportMemory(SqliteConnection conn, CancellationToken ct)
    {
        var items = await FetchJsonAsync<PCPartMemory>("memory", ct);
        if (items == null) return 0;

        Debug.WriteLine($"  Memory: {items.Count} baixados");

        var count = 0;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO MemoryModels (Id, StandardId, ManufacturerId, PartNumber, CapacityMb, SpeedMhz, FormFactor, Ecc, Registered)
            VALUES ($id, $sid, $mid, $part, $cap, $spd, $ff, 0, 0)
            """;

        foreach (var mem in items)
        {
            if (ct.IsCancellationRequested) break;

            var manu = ExtractManufacturer(mem.Name ?? "", conn);
            var (speedMhz, standardId) = ParseMemorySpeed(mem.Speed);
            var capacityMb = ParseMemoryCapacity(mem.Modules);
            if (capacityMb == null && speedMhz == null) continue;

            var partNumber = mem.Name ?? "";
            var id = $"{manu.Id}.dimm.{NormalizeId(mem.Name ?? "")}";

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$sid", standardId ?? "ddr4");
            cmd.Parameters.AddWithValue("$mid", manu.Id);
            cmd.Parameters.AddWithValue("$part", partNumber);
            cmd.Parameters.AddWithValue("$cap", (object?)capacityMb ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$spd", (object?)speedMhz ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ff", "DIMM");
            cmd.ExecuteNonQuery();
            count++;
        }

        Debug.WriteLine($"  Memory: {count} importados");
        return count;
    }

    private static (int? SpeedMhz, string? StandardId) ParseMemorySpeed(object? speed)
    {
        if (speed == null) return (null, null);

        var ints = ExtractIntList(speed);
        if (ints == null || ints.Count == 0) return (null, null);

        // Format [5, 6000] = DDR generation + speed in MHz
        if (ints.Count >= 2)
        {
            var gen = ints[0];
            var mhz = ints[1];
            var std = gen switch
            {
                5 => "ddr5",
                4 => "ddr4",
                3 => "ddr3",
                2 => "ddr2",
                _ => null
            };
            return (mhz, std);
        }

        // Single value = speed in MHz
        var singleMhz = ints[0];
        var deducedStd = singleMhz switch
        {
            >= 4800 => "ddr5",
            >= 2133 => "ddr4",
            >= 1066 => "ddr3",
            >= 400 => "ddr2",
            _ => "ddr"
        };
        return (singleMhz, deducedStd);
    }

    private static int? ParseMemoryCapacity(object? modules)
    {
        if (modules == null) return null;

        var ints = ExtractIntList(modules);
        if (ints == null || ints.Count == 0) return null;

        if (ints.Count >= 2)
        {
            // [2, 16] = 2 modules × 16GB each = 32GB total
            var count = ints[0];
            var perModule = ints[1];
            return count * perModule * 1024; // GB to MB
        }

        // Single value
        var val = ints[0];
        if (val >= 1 && val <= 8) return null; // It's module count, not capacity
        return val * 1024; // Assume GB value
    }

    private static List<int>? ExtractIntList(object? value)
    {
        if (value == null) return null;
        if (value is List<int> list) return list;
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
                return je.EnumerateArray().Select(e => e.GetInt32()).ToList();
            if (je.ValueKind == JsonValueKind.Number)
                return [je.GetInt32()];
        }
        return null;
    }

    // ── Storage (6,461 items) ──────────────────────────────────────────────

    private async Task<int> ImportStorage(SqliteConnection conn, CancellationToken ct)
    {
        var items = await FetchJsonAsync<PCPartStorage>("internal-hard-drive", ct);
        if (items == null) return 0;

        Debug.WriteLine($"  Storage: {items.Count} baixados");

        var count = 0;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO StorageDevices (Id, ControllerId, ManufacturerId, Name, FormFactor, CapacityGb)
            VALUES ($id, $cid, $mid, $name, $ff, $cap)
            """;

        foreach (var dev in items)
        {
            if (ct.IsCancellationRequested) break;

            var manu = ExtractManufacturer(dev.Name ?? "", conn);
            var ctrlId = (dev.Interface ?? "").Contains("NVMe", StringComparison.OrdinalIgnoreCase)
                || (dev.Interface ?? "").Contains("PCIe", StringComparison.OrdinalIgnoreCase)
                ? "generic.nvme"
                : "generic.ahci";
            var ff = NormalizeStorageFormFactor(dev.FormFactor ?? "");
            var name = dev.Name ?? "";
            var id = $"{manu.Id}.storage.{NormalizeId(name)}";

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$cid", ctrlId);
            cmd.Parameters.AddWithValue("$mid", manu.Id);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$ff", (object?)ff ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cap", dev.Capacity.HasValue ? (object)(int)dev.Capacity.Value : DBNull.Value);
            cmd.ExecuteNonQuery();
            count++;
        }

        // Re-insert storage sensor mappings
        count += ReSeedStorageSensorMappings(conn, ct);

        Debug.WriteLine($"  Storage: {count} total (incl. sensor mappings)");
        return count;
    }

    private static int ReSeedStorageSensorMappings(SqliteConnection conn, CancellationToken ct)
    {
        var count = 0;
        var mappings = new[] { ("Temperature", "Temperature"), ("Load", "Total Activity"), ("Load", "Read Activity"), ("Load", "Write Activity") };

        using var delete = conn.CreateCommand();
        delete.CommandText = "DELETE FROM StorageSensorMappings";
        delete.ExecuteNonQuery();

        using var query = conn.CreateCommand();
        query.CommandText = "SELECT Id FROM StorageDevices";

        var ids = new List<string>();
        using (var reader = query.ExecuteReader())
            while (reader.Read()) ids.Add(reader.GetString(0));

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO StorageSensorMappings (StorageDeviceId, SensorType, PreferredName, Priority) VALUES ($id, $type, $pref, $pri)";

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            var pri = 0;
            foreach (var (type, pref) in mappings)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$id", id);
                cmd.Parameters.AddWithValue("$type", type);
                cmd.Parameters.AddWithValue("$pref", pref);
                cmd.Parameters.AddWithValue("$pri", pri++);
                cmd.ExecuteNonQuery();
                count++;
            }
        }

        return count;
    }

    private static string? NormalizeStorageFormFactor(object? ff)
    {
        if (ff == null) return null;
        var str = ff.ToString()?.Trim() ?? "";
        if (string.IsNullOrEmpty(str)) return null;
        if (str.Contains("M.2")) return "M.2";
        if (str.Contains("2.5")) return "2.5\"";
        if (str.Contains("3.5")) return "3.5\"";
        if (str.Contains("HHHL") || str.Contains("add-in")) return "HHHL";
        if (str.Contains("mSATA")) return "mSATA";
        if (str.Contains("U.2")) return "U.2";
        if (str.Contains("PCIe") && str.Contains("x")) return "HHHL";
        // Numeric values like "3.5" or "2.5" get normalized here
        if (str == "3.5") return "3.5\"";
        if (str == "2.5") return "2.5\"";
        return str;
    }

    // ── Power Supplies (3,438 items) ───────────────────────────────────────

    private async Task<int> ImportPowerSupplies(SqliteConnection conn, CancellationToken ct)
    {
        var items = await FetchJsonAsync<PCPartPSU>("power-supply", ct);
        if (items == null) return 0;

        Debug.WriteLine($"  PSUs: {items.Count} baixados");

        using var clear = conn.CreateCommand();
        clear.CommandText = "DELETE FROM PsuSensorMappings; DELETE FROM PowerSupplies";
        clear.ExecuteNonQuery();

        var count = 0;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO PowerSupplies (Id, ManufacturerId, Name, Type, Wattage, Efficiency, Modular)
            VALUES ($id, $mid, $name, $type, $watt, $eff, $mod)
            """;

        foreach (var psu in items)
        {
            if (ct.IsCancellationRequested) break;

            var manu = ExtractManufacturer(psu.Name ?? "", conn);
            var id = $"{manu.Id}.psu.{NormalizeId(psu.Name ?? "")}";

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$mid", manu.Id);
            cmd.Parameters.AddWithValue("$name", psu.Name ?? "");
            cmd.Parameters.AddWithValue("$type", (object?)(psu.Type ?? "ATX") ?? "ATX");
            cmd.Parameters.AddWithValue("$watt", (object?)psu.Wattage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$eff", (object?)NormalizeEfficiency(psu.Efficiency) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$mod", (object?)NormalizeModular(psu.Modular) ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            count++;
        }

        Debug.WriteLine($"  PSUs: {count} importados");
        return count;
    }

    private static string? NormalizeEfficiency(string? eff)
    {
        if (string.IsNullOrEmpty(eff)) return null;
        return eff.ToLowerInvariant() switch
        {
            "bronze" => "80+ Bronze",
            "silver" => "80+ Silver",
            "gold" => "80+ Gold",
            "platinum" => "80+ Platinum",
            "titanium" => "80+ Titanium",
            "white" => "80+ White",
            _ => char.ToUpperInvariant(eff[0]) + eff[1..]
        };
    }

    private static string? NormalizeModular(object? modular)
    {
        if (modular == null) return null;
        if (modular is bool b) return b ? "Full" : "No";
        var str = modular.ToString()?.Trim();
        if (string.IsNullOrEmpty(str)) return null;
        // Accept common short forms
        var lower = str.ToLowerInvariant();
        if (lower == "yes" || lower == "y" || lower == "true") return "Full";
        if (lower == "no" || lower == "n" || lower == "false") return "No";
        if (lower == "semi" || lower == "partial") return "Semi";
        return str;
    }

    // ── Network Devices (533 items) ────────────────────────────────────────

    private async Task<int> ImportNetworkDevices(SqliteConnection conn, CancellationToken ct)
    {
        var wired = await FetchJsonAsync<PCPartNetwork>("wired-network-card", ct) ?? [];
        var wireless = await FetchJsonAsync<PCPartNetwork>("wireless-network-card", ct) ?? [];

        Debug.WriteLine($"  Network: {wired.Count} wired + {wireless.Count} wireless baixados");

        using var clear = conn.CreateCommand();
        clear.CommandText = "DELETE FROM NetworkSensorMappings; DELETE FROM NetworkDevices";
        clear.ExecuteNonQuery();

        var count = 0;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO NetworkDevices (Id, ManufacturerId, Name, Interface, WirelessStandard, DeviceType)
            VALUES ($id, $mid, $name, $iface, $ws, $type)
            """;

        foreach (var dev in wired)
        {
            if (ct.IsCancellationRequested) break;
            var manu = ExtractManufacturer(dev.Name ?? "", conn);
            var id = $"{manu.Id}.nic.{NormalizeId(dev.Name ?? "")}";

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$mid", manu.Id);
            cmd.Parameters.AddWithValue("$name", dev.Name ?? "");
            cmd.Parameters.AddWithValue("$iface", (object?)dev.Interface ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ws", DBNull.Value);
            cmd.Parameters.AddWithValue("$type", "Wired");
            cmd.ExecuteNonQuery();
            count++;
        }

        foreach (var dev in wireless)
        {
            if (ct.IsCancellationRequested) break;
            var manu = ExtractManufacturer(dev.Name ?? "", conn);
            var id = $"{manu.Id}.wifi.{NormalizeId(dev.Name ?? "")}";

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$mid", manu.Id);
            cmd.Parameters.AddWithValue("$name", dev.Name ?? "");
            cmd.Parameters.AddWithValue("$iface", (object?)dev.Interface ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ws", (object?)dev.Protocol ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$type", "Wireless");
            cmd.ExecuteNonQuery();
            count++;
        }

        Debug.WriteLine($"  Network: {count} importados");
        return count;
    }

    // ── Sensor Mappings for PSU ────────────────────────────────────────────

    private int SeedPsuSensorMappings(SqliteConnection conn, CancellationToken ct)
    {
        var count = 0;

        var mappings = new (string Type, string[] Names)[]
        {
            ("Temperature", ["Temperature", "PSU Temperature", "Internal Temperature"]),
            ("Fan", ["Fan", "PSU Fan"]),
            ("Power", ["Total Power", "PSU Power", "Input Power"]),
            ("Voltage", ["+12V", "+5V", "+3.3V"]),
            ("Voltage", ["+12V", "12V Rail"]),
            ("Voltage", ["+5V", "5V Rail"]),
            ("Voltage", ["+3.3V", "3.3V Rail"]),
            ("Current", ["+12V Current", "Input Current"]),
        };

        using var query = conn.CreateCommand();
        query.CommandText = "SELECT Id FROM PowerSupplies";

        var ids = new List<string>();
        using (var reader = query.ExecuteReader())
            while (reader.Read()) ids.Add(reader.GetString(0));

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO PsuSensorMappings (PowerSupplyId, SensorType, PreferredName, Priority) VALUES ($id, $type, $pref, $pri)";

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            var pri = 0;
            foreach (var (type, names) in mappings)
            {
                foreach (var name in names)
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.Parameters.AddWithValue("$type", type);
                    cmd.Parameters.AddWithValue("$pref", name);
                    cmd.Parameters.AddWithValue("$pri", pri++);
                    cmd.ExecuteNonQuery();
                    count++;
                }
            }
        }

        return count;
    }

    // ── Sensor Mappings for Network ────────────────────────────────────────

    private static int SeedNetworkSensorMappings(SqliteConnection conn, CancellationToken ct)
    {
        var count = 0;

        var wiredMappings = new (string Type, string[] Names)[]
        {
            ("Load", ["Total Activity", "Download Activity", "Upload Activity"]),
            ("Data", ["Bytes Received", "Bytes Sent", "Bytes Total"]),
        };

        var wirelessExtra = new (string Type, string[])[]
        {
            ("Signal", ["Signal Strength", "Signal Quality"]),
        };

        using var query = conn.CreateCommand();
        query.CommandText = "SELECT Id, DeviceType FROM NetworkDevices";

        var devices = new List<(string Id, string Type)>();
        using (var reader = query.ExecuteReader())
            while (reader.Read()) devices.Add((reader.GetString(0), reader.GetString(1)));

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO NetworkSensorMappings (NetworkDeviceId, SensorType, PreferredName, Priority) VALUES ($id, $type, $pref, $pri)";

        foreach (var (devId, devType) in devices)
        {
            if (ct.IsCancellationRequested) break;
            var pri = 0;

            foreach (var (type, names) in wiredMappings)
            {
                foreach (var name in names)
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("$id", devId);
                    cmd.Parameters.AddWithValue("$type", type);
                    cmd.Parameters.AddWithValue("$pref", name);
                    cmd.Parameters.AddWithValue("$pri", pri++);
                    cmd.ExecuteNonQuery();
                    count++;
                }
            }

            if (devType == "Wireless")
            {
                foreach (var (type, names) in wirelessExtra)
                {
                    foreach (var name in names)
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("$id", devId);
                        cmd.Parameters.AddWithValue("$type", type);
                        cmd.Parameters.AddWithValue("$pref", name);
                        cmd.Parameters.AddWithValue("$pri", pri++);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
        }

        return count;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<List<T>?> FetchJsonAsync<T>(string category, CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}/{category}.json";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<T>>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"  Erro ao baixar {category}: {ex.Message}");
            return null;
        }
    }

    private (string Id, string Name) ExtractManufacturer(string name, SqliteConnection? conn = null)
    {
        if (string.IsNullOrEmpty(name)) return ("unknown", "Unknown");

        // Known manufacturer prefixes (sorted by length desc for greedy matching)
        var known = new (string Id, string Name, string[] Prefixes)[]
        {
            ("westerndigital", "Western Digital", ["Western Digital", "WD ", "Wd "]),
            ("siliconpower", "Silicon Power", ["Silicon Power", "SP "]),
            ("thermaltake", "Thermaltake", ["Thermaltake"]),
            ("coolermaster", "Cooler Master", ["Cooler Master"]),
            ("bequiet", "be quiet!", ["be quiet!", "bequiet"]),
            ("superflower", "Super Flower", ["Super Flower"]),
            ("silverstone", "SilverStone", ["SilverStone", "Silverstone"]),
            ("teamgroup", "TEAMGROUP", ["TEAMGROUP", "Team Group", "Team ", "T-Force"]),
            ("deepcool", "DeepCool", ["DeepCool", "Deep Cool"]),
            ("alphacool", "Alphacool", ["Alphacool"]),
            ("raijintek", "Raijintek", ["Raijintek"]),
            ("fractal", "Fractal Design", ["Fractal Design"]),
            ("lianli", "Lian Li", ["Lian Li"]),
            ("seasonic", "Seasonic", ["Seasonic"]),
            ("antec", "Antec", ["Antec"]),
            ("enermax", "Enermax", ["Enermax"]),
            ("nzxt", "NZXT", ["NZXT"]),
            ("noctua", "Noctua", ["Noctua"]),
            ("arctic", "Arctic", ["Arctic"]),
            ("zalman", "Zalman", ["Zalman"]),
            ("scythe", "Scythe", ["Scythe"]),
            ("ekwb", "EKWB", ["EKWB", "EK "]),
            ("rosewill", "Rosewill", ["Rosewill"]),
            ("tp-link", "TP-Link", ["TP-Link"]),
            ("asus", "ASUS", ["Asus ", "ASUS "]),
            ("asrock", "ASRock", ["ASRock", "Asrock"]),
            ("gigabyte", "Gigabyte", ["Gigabyte", "GIGABYTE"]),
            ("biostar", "Biostar", ["Biostar"]),
            ("colorful", "Colorful", ["Colorful"]),
            ("maxsun", "MAXSUN", ["MAXSUN", "Maxsun"]),
            ("msi", "MSI", ["MSI "]),
            ("evga", "EVGA", ["EVGA"]),
            ("fsp", "FSP Group", ["FSP "]),
            ("corsair", "Corsair", ["Corsair"]),
            ("kingston", "Kingston", ["Kingston"]),
            ("gskill", "G.Skill", ["G.Skill", "G.SKILL", "G Skill"]),
            ("crucial", "Crucial", ["Crucial"]),
            ("patriot", "Patriot", ["Patriot"]),
            ("pny", "PNY", ["PNY "]),
            ("mushkin", "Mushkin", ["Mushkin"]),
            ("geil", "GeIL", ["GeIL"]),
            ("ocz", "OCZ", ["OCZ "]),
            ("xpg", "XPG", ["XPG "]),
            ("adata", "ADATA", ["ADATA"]),
            ("samsung", "Samsung", ["Samsung"]),
            ("skhynix", "SK Hynix", ["SK Hynix"]),
            ("micron", "Micron", ["Micron "]),
            ("intel", "Intel", ["Intel"]),
            ("amd", "AMD", ["AMD "]),
            ("hp", "HP", ["HP "]),
            ("dell", "Dell", ["Dell "]),
            ("lenovo", "Lenovo", ["Lenovo "]),
            ("acer", "Acer", ["Acer "]),
            ("sony", "Sony", ["Sony "]),
            ("lg", "LG", ["LG "]),
            ("seagate", "Seagate", ["Seagate"]),
            ("toshiba", "Toshiba", ["Toshiba"]),
            ("sandisk", "SanDisk", ["SanDisk"]),
            ("foxconn", "Foxconn", ["Foxconn"]),
            ("micron", "Micron", ["Micron"]),
        };

        foreach (var (id, displayName, prefixes) in known)
        {
            foreach (var prefix in prefixes)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    EnsureManufacturerInline(conn, id, displayName);
                    return (id, displayName);
                }
            }
        }

        // Fallback: take first word as manufacturer
        var firstWord = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var cleanId = firstWord.ToLowerInvariant().TrimEnd(',', '.').TrimEnd(':');
        var display = firstWord.TrimEnd(',', '.', ':');
        EnsureManufacturerInline(conn, cleanId, display);
        return (cleanId, display);
    }

    private static void EnsureManufacturerInline(SqliteConnection? conn, string id, string displayName)
    {
        if (conn == null) return;
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO Manufacturers (Id, DisplayName, ShortName) VALUES ($id, $dn, $sn)";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$dn", displayName);
            cmd.Parameters.AddWithValue("$sn", id);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Ignore se falhar (race condition ou outro erro)
        }
    }

    private static string? NormalizeFormFactor(string ff)
    {
        if (string.IsNullOrEmpty(ff)) return null;
        var lower = ff.ToLowerInvariant();
        if (lower.Contains("mini itx") || lower.Contains("mini-itx")) return "Mini-ITX";
        if (lower.Contains("micro atx") || lower.Contains("micro-atx") || lower.Contains("matx") || lower.Contains("m-atx")) return "mATX";
        if (lower.Contains("atx")) return "ATX";
        if (lower.Contains("e-atx") || lower.Contains("eatx") || lower.Contains("extended")) return "E-ATX";
        if (lower.Contains("mini") || lower.Contains("small")) return "SFF";
        if (lower.Contains("proprietary")) return "Proprietary";
        if (lower.Contains("thin")) return "Thin-ITX";
        if (lower.Contains("nlx")) return "NLX";
        if (lower.Contains("lpx")) return "LPX";
        if (lower.Contains("btx")) return "BTX";
        if (lower.Contains("notebook") || lower.Contains("laptop")) return "Notebook";
        return ff;
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
            .Replace("[", "")
            .Replace("]", "")
            .Replace("--", "-")
            .Replace("---", "-")
            .Trim('-');
    }
}

// ── DTOs (PCPartPicker JSON structure) ──────────────────────────────────

internal sealed class PCPartMotherboard
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("socket")] public string? Socket { get; set; }
    [JsonPropertyName("form_factor")] public string? FormFactor { get; set; }
    [JsonPropertyName("max_memory")] public int? MaxMemory { get; set; }
    [JsonPropertyName("memory_slots")] public int? MemorySlots { get; set; }
    [JsonPropertyName("color")] public string? Color { get; set; }
}

internal sealed class PCPartMemory
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("speed")] public object? Speed { get; set; }
    [JsonPropertyName("modules")] public object? Modules { get; set; }
    [JsonPropertyName("cas_latency")] public double? CasLatency { get; set; }
    [JsonPropertyName("first_word_latency")] public double? FirstWordLatency { get; set; }
    [JsonPropertyName("color")] public string? Color { get; set; }
}

internal sealed class PCPartStorage
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("capacity")] public double? Capacity { get; set; }
    [JsonPropertyName("type")] public object? Type { get; set; }
    [JsonPropertyName("cache")] public int? Cache { get; set; }
    [JsonPropertyName("form_factor")] public object? FormFactor { get; set; }
    [JsonPropertyName("interface")] public string? Interface { get; set; }
}

internal sealed class PCPartPSU
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("efficiency")] public string? Efficiency { get; set; }
    [JsonPropertyName("wattage")] public int? Wattage { get; set; }
    [JsonPropertyName("modular")] public object? Modular { get; set; }
    [JsonPropertyName("color")] public string? Color { get; set; }
}

internal sealed class PCPartNetwork
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("interface")] public string? Interface { get; set; }
    [JsonPropertyName("protocol")] public string? Protocol { get; set; }
    [JsonPropertyName("color")] public string? Color { get; set; }
}
