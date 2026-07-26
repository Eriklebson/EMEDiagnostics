using System.Diagnostics;
using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Services;

namespace EME.HardwareDatabase.Seed;

public sealed class ReferenceDataSeeder : ISeedImporter
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public string SourceName => "Reference Data";

    public ReferenceDataSeeder(DatabaseConnectionFactory connectionFactory)
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

            EnsureManufacturers(conn);
            imported += SeedSuperIoChips(conn, ct);
            imported += SeedMemoryStandards(conn, ct);
            imported += SeedMemoryModels(conn, ct);
            imported += SeedStorageControllers(conn, ct);
            imported += SeedStorageDevices(conn, ct);
            imported += SeedMotherboards(conn, ct);
            imported += SeedKnownIssues(conn, ct);

            tx.Commit();
        }
        catch (Exception ex)
        {
            errors.Add($"Erro no ReferenceDataSeeder: {ex.Message}");
        }

        return Task.FromResult(new SeedResult(SourceName, imported, errors.Count,
            errors.Count > 0 ? errors.AsReadOnly() : null));
    }

    private static void EnsureManufacturers(SqliteConnection conn)
    {
        var manufacturers = new (string Id, string DisplayName)[]
        {
            ("nuvoton", "Nuvoton Technology"),
            ("ite", "ITE Tech"),
            ("fintek", "Fintek"),
            ("microchip", "Microchip Technology"),
            ("winbond", "Winbond Electronics"),
            ("samsung", "Samsung"),
            ("micron", "Micron Technology"),
            ("skhynix", "SK Hynix"),
            ("kingston", "Kingston Technology"),
            ("corsair", "Corsair Memory"),
            ("gskill", "G.Skill"),
            ("crucial", "Crucial"),
            ("westerndigital", "Western Digital"),
            ("seagate", "Seagate Technology"),
            ("toshiba", "Toshiba"),
            ("sandisk", "SanDisk"),
            ("adata", "ADATA Technology"),
            ("intel", "Intel"),
            ("asrock", "ASRock"),
            ("gigabyte", "Gigabyte Technology"),
            ("msi", "Micro-Star International"),
            ("asus", "ASUSTeK Computer"),
            ("biostar", "Biostar"),
            ("evga", "EVGA Corporation"),
            ("supermicro", "Super Micro Computer"),
            ("lenovo", "Lenovo"),
            ("dell", "Dell"),
            ("hp", "HP Inc"),
            ("foxconn", "Foxconn"),
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

    private static int SeedSuperIoChips(SqliteConnection conn, CancellationToken ct)
    {
        var count = 0;

        var chips = new (string Id, string ManufacturerId, string Name)[]
        {
            ("nuvoton.nct6798d", "nuvoton", "NCT6798D"),
            ("nuvoton.nct6797d", "nuvoton", "NCT6797D"),
            ("nuvoton.nct6796d", "nuvoton", "NCT6796D"),
            ("nuvoton.nct6795d", "nuvoton", "NCT6795D"),
            ("nuvoton.nct6793d", "nuvoton", "NCT6793D"),
            ("nuvoton.nct6792d", "nuvoton", "NCT6792D"),
            ("nuvoton.nct6791d", "nuvoton", "NCT6791D"),
            ("nuvoton.nct6687", "nuvoton", "NCT6687"),
            ("nuvoton.nct6686", "nuvoton", "NCT6686"),
            ("nuvoton.nct5585", "nuvoton", "NCT5585D"),
            ("nuvoton.nct5572", "nuvoton", "NCT5572D"),
            ("nuvoton.nct5565", "nuvoton", "NCT5565"),
            ("nuvoton.nct5537", "nuvoton", "NCT5537D"),
            ("nuvoton.nct5535", "nuvoton", "NCT5535D"),
            ("nuvoton.nct5104", "nuvoton", "NCT5104D"),
            ("ite.it8792e", "ite", "IT8792E"),
            ("ite.it8786e", "ite", "IT8786E"),
            ("ite.it8783e", "ite", "IT8783E"),
            ("ite.it8781e", "ite", "IT8781E"),
            ("ite.it8728f", "ite", "IT8728F"),
            ("ite.it8725f", "ite", "IT8725F"),
            ("ite.it8720f", "ite", "IT8720F"),
            ("ite.it8718f", "ite", "IT8718F"),
            ("ite.it8686e", "ite", "IT8686E"),
            ("ite.it8688e", "ite", "IT8688E"),
            ("ite.it8692e", "ite", "IT8692E"),
            ("ite.it8613e", "ite", "IT8613E"),
            ("ite.it8625e", "ite", "IT8625E"),
            ("ite.it8620e", "ite", "IT8620E"),
            ("ite.it8655e", "ite", "IT8655E"),
            ("fintek.f71878ad", "fintek", "F71878AD"),
            ("fintek.f71869ed", "fintek", "F71869ED"),
            ("fintek.f71869", "fintek", "F71869"),
            ("fintek.f71808e", "fintek", "F71808E"),
            ("fintek.f71858", "fintek", "F71858"),
            ("fintek.f71882", "fintek", "F71882"),
            ("fintek.f71862", "fintek", "F71862"),
            ("fintek.f81768", "fintek", "F81768"),
            ("microchip.emc6d103", "microchip", "EMC6D103"),
            ("microchip.emc6d103s", "microchip", "EMC6D103S"),
            ("winbond.w83627", "winbond", "W83627HF"),
            ("winbond.w83667", "winbond", "W83667HG"),
            ("winbond.w83677", "winbond", "W83677HG"),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO SuperIoChips (Id, ManufacturerId, Name)
            VALUES ($id, $mid, $name)
            """;

        foreach (var (id, mid, name) in chips)
        {
            if (ct.IsCancellationRequested) break;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$mid", mid);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.ExecuteNonQuery();
            count++;
        }

        return count;
    }

    private static int SeedMemoryStandards(SqliteConnection conn, CancellationToken ct)
    {
        var count = 0;

        var standards = new (string Id, string Name, string MemoryType, int? MaxMhz)[]
        {
            ("ddr", "DDR (Double Data Rate)", "DDR", 400),
            ("ddr2", "DDR2", "DDR2", 1066),
            ("ddr3", "DDR3", "DDR3", 2133),
            ("ddr3l", "DDR3L", "DDR3", 2133),
            ("ddr4", "DDR4", "DDR4", 5333),
            ("ddr5", "DDR5", "DDR5", 8400),
            ("lpddr3", "LPDDR3", "LPDDR3", 2133),
            ("lpddr4", "LPDDR4", "LPDDR4", 4266),
            ("lpddr5", "LPDDR5", "LPDDR5", 6400),
            ("gddr3", "GDDR3", "GDDR3", 2500),
            ("gddr5", "GDDR5", "GDDR5", 7000),
            ("gddr5x", "GDDR5X", "GDDR5X", 11200),
            ("gddr6", "GDDR6", "GDDR6", 16000),
            ("gddr6x", "GDDR6X", "GDDR6X", 21000),
            ("gddr7", "GDDR7", "GDDR7", 32000),
            ("hbm", "HBM (High Bandwidth Memory)", "HBM", 2000),
            ("hbm2", "HBM2", "HBM2", 2400),
            ("hbm2e", "HBM2e", "HBM2e", 3200),
            ("hbm3", "HBM3", "HBM3", 6400),
            ("sram", "SRAM", "SRAM", null),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO MemoryStandards (Id, Name, MemoryType, MaxSpeedMhz)
            VALUES ($id, $name, $type, $max)
            """;

        foreach (var (id, name, type, max) in standards)
        {
            if (ct.IsCancellationRequested) break;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$type", type);
            cmd.Parameters.AddWithValue("$max", (object?)max ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            count++;
        }

        return count;
    }

    private static int SeedMemoryModels(SqliteConnection conn, CancellationToken ct)
    {
        var count = 0;

        var modules = new (string Id, string StdId, string ManId, string Part, int? Cap, int? Speed, string? Factor, bool Ecc, bool Reg)[]
        {
            // DDR4 common modules
            ("ddr4.corsair.8gb-2133", "ddr4", "corsair", "CMV8GX4M1A2133C15", 8192, 2133, "DIMM", false, false),
            ("ddr4.corsair.16gb-3200", "ddr4", "corsair", "CMK16GX4M2B3200C16", 16384, 3200, "DIMM", false, false),
            ("ddr4.corsair.32gb-3600", "ddr4", "corsair", "CMK32GX4M2D3600C18", 32768, 3600, "DIMM", false, false),
            ("ddr4.corsair.8gb-2666", "ddr4", "corsair", "CMV8GX4M1A2666C16", 8192, 2666, "DIMM", false, false),
            ("ddr4.gskill.16gb-3200", "ddr4", "gskill", "F4-3200C16D-16GVKB", 16384, 3200, "DIMM", false, false),
            ("ddr4.gskill.32gb-3600", "ddr4", "gskill", "F4-3600C16D-32GTZNC", 32768, 3600, "DIMM", false, false),
            ("ddr4.gskill.8gb-2400", "ddr4", "gskill", "F4-2400C15S-8GNT", 8192, 2400, "DIMM", false, false),
            ("ddr4.kingston.16gb-3200", "ddr4", "kingston", "KF432C16BB/16", 16384, 3200, "DIMM", false, false),
            ("ddr4.kingston.32gb-2666", "ddr4", "kingston", "KVR26N19D8/32", 32768, 2666, "DIMM", false, false),
            ("ddr4.samsung.8gb-2133", "ddr4", "samsung", "M378A1K43CB2-CTD", 8192, 2133, "DIMM", false, false),
            ("ddr4.samsung.16gb-2666", "ddr4", "samsung", "M378A2K43CB1-CTD", 16384, 2666, "DIMM", false, false),
            ("ddr4.samsung.32gb-3200", "ddr4", "samsung", "M378A4G43MB3-CTD", 32768, 3200, "DIMM", false, false),
            ("ddr4.micron.8gb-2400", "ddr4", "micron", "MT40A1G8RH-083E", 8192, 2400, "DIMM", false, false),
            ("ddr4.skhynix.16gb-2400", "ddr4", "skhynix", "HMA82GU6AFR8N-UH", 16384, 2400, "DIMM", false, false),
            ("ddr4.skhynix.32gb-2666", "ddr4", "skhynix", "HMA84GR7JJR4N-WM", 32768, 2666, "DIMM", false, false),
            // DDR4 ECC
            ("ddr4.samsung.16gb-2666-ecc", "ddr4", "samsung", "M393A2K43DB1-CVF", 16384, 2666, "DIMM", true, false),
            ("ddr4.skhynix.32gb-2933-ecc", "ddr4", "skhynix", "HMA84GR7DJR4N-XN", 32768, 2933, "DIMM", true, false),
            ("ddr4.micron.64gb-3200-ecc", "ddr4", "micron", "MTA18ASF4G72PZ-3G2E1", 65536, 3200, "DIMM", true, false),
            // SO-DIMM DDR4
            ("ddr4.samsung.8gb-2666-sodimm", "ddr4", "samsung", "M471A1K43CB1-CTD", 8192, 2666, "SO-DIMM", false, false),
            ("ddr4.kingston.16gb-3200-sodimm", "ddr4", "kingston", "KF432S20IB/16", 16384, 3200, "SO-DIMM", false, false),
            // DDR5 common modules
            ("ddr5.corsair.16gb-5600", "ddr5", "corsair", "CMK16GX5M1B5600C40", 16384, 5600, "DIMM", false, false),
            ("ddr5.corsair.32gb-6000", "ddr5", "corsair", "CMK32GX5M2D6000C36", 32768, 6000, "DIMM", false, false),
            ("ddr5.corsair.64gb-5200", "ddr5", "corsair", "CMK64GX5M2B5200C40", 65536, 5200, "DIMM", false, false),
            ("ddr5.gskill.32gb-6000", "ddr5", "gskill", "F5-6000J3636F16GX2-TZ5N", 32768, 6000, "DIMM", false, false),
            ("ddr5.gskill.48gb-7200", "ddr5", "gskill", "F5-7200J3646F24GX2-TZ5RK", 49152, 7200, "DIMM", false, false),
            ("ddr5.gskill.16gb-5600", "ddr5", "gskill", "F5-5600J2834F16GX2-RS5K", 16384, 5600, "DIMM", false, false),
            ("ddr5.kingston.16gb-4800", "ddr5", "kingston", "KF548S32-16", 16384, 4800, "DIMM", false, false),
            ("ddr5.kingston.32gb-5600", "ddr5", "kingston", "KF556C36BBE-32", 32768, 5600, "DIMM", false, false),
            ("ddr5.samsung.16gb-4800", "ddr5", "samsung", "M323R1GA4BB0-CQK", 16384, 4800, "DIMM", false, false),
            ("ddr5.skhynix.32gb-5600", "ddr5", "skhynix", "HMCG78AGBSA095N", 32768, 5600, "DIMM", false, false),
            // SO-DIMM DDR5
            ("ddr5.samsung.16gb-4800-sodimm", "ddr5", "samsung", "M425R1GB4BB0-CQK", 16384, 4800, "SO-DIMM", false, false),
            ("ddr5.skhynix.32gb-5600-sodimm", "ddr5", "skhynix", "HMCG78MEBSA095N", 32768, 5600, "SO-DIMM", false, false),
            // DDR3 legacy
            ("ddr3.kingston.4gb-1333", "ddr3", "kingston", "KVR1333D3N9/4G", 4096, 1333, "DIMM", false, false),
            ("ddr3.kingston.8gb-1600", "ddr3", "kingston", "KVR16N11/8", 8192, 1600, "DIMM", false, false),
            ("ddr3.gskill.8gb-1600", "ddr3", "gskill", "F3-1600C11D-8GNT", 8192, 1600, "DIMM", false, false),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO MemoryModels (Id, StandardId, ManufacturerId, PartNumber, CapacityMb, SpeedMhz, FormFactor, Ecc, Registered)
            VALUES ($id, $sid, $mid, $part, $cap, $spd, $ff, $ecc, $reg)
            """;

        foreach (var (id, sid, mid, part, cap, speed, ff, ecc, reg) in modules)
        {
            if (ct.IsCancellationRequested) break;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$sid", sid);
            cmd.Parameters.AddWithValue("$mid", mid);
            cmd.Parameters.AddWithValue("$part", part);
            cmd.Parameters.AddWithValue("$cap", (object?)cap ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$spd", (object?)speed ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ff", (object?)ff ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ecc", ecc ? 1 : 0);
            cmd.Parameters.AddWithValue("$reg", reg ? 1 : 0);
            cmd.ExecuteNonQuery();
            count++;
        }

        return count;
    }

    private static int SeedStorageControllers(SqliteConnection conn, CancellationToken ct)
    {
        var count = 0;

        var controllers = new (string Id, string ManId, string Name, string Interface)[]
        {
            ("intel.ahci", "intel", "Intel SATA AHCI Controller", "SATA"),
            ("intel.nvme", "intel", "Intel NVMe Controller", "NVMe"),
            ("amd.ahci", "amd", "AMD SATA AHCI Controller", "SATA"),
            ("amd.nvme", "amd", "AMD NVMe Controller", "NVMe"),
            ("samsung.nvme", "samsung", "Samsung NVMe Controller", "NVMe"),
            ("skhynix.nvme", "skhynix", "SK Hynix NVMe Controller", "NVMe"),
            ("toshiba.ahci", "toshiba", "Toshiba SATA AHCI Controller", "SATA"),
            ("westerndigital.nvme", "westerndigital", "Western Digital NVMe Controller", "NVMe"),
            ("sandisk.nvme", "sandisk", "SanDisk NVMe Controller", "NVMe"),
            ("micron.nvme", "micron", "Micron NVMe Controller", "NVMe"),
            ("kingston.nvme", "kingston", "Kingston NVMe Controller", "NVMe"),
            ("intel.optane", "intel", "Intel Optane Memory Controller", "NVMe"),
            ("nvidia.nvme", "nvidia", "NVIDIA NVMe Controller (nForce)", "NVMe"),
            ("generic.ahci", "intel", "Standard SATA AHCI Controller", "SATA"),
            ("generic.nvme", "intel", "Standard NVM Express Controller", "NVMe"),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO StorageControllers (Id, ManufacturerId, Name, Interface)
            VALUES ($id, $mid, $name, $iface)
            """;

        foreach (var (id, mid, name, iface) in controllers)
        {
            if (ct.IsCancellationRequested) break;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$mid", mid);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$iface", iface);
            cmd.ExecuteNonQuery();
            count++;
        }

        return count;
    }

    private static int SeedStorageDevices(SqliteConnection conn, CancellationToken ct)
    {
        var count = 0;

        var devices = new (string Id, string? CtrlId, string ManId, string Name, string? Factor, int? CapGb)[]
        {
            // NVMe SSDs - Samsung
            ("samsung.990-pro-1tb", "samsung.nvme", "samsung", "Samsung SSD 990 PRO 1TB", "M.2", 1024),
            ("samsung.990-pro-2tb", "samsung.nvme", "samsung", "Samsung SSD 990 PRO 2TB", "M.2", 2048),
            ("samsung.990-evo-1tb", "samsung.nvme", "samsung", "Samsung SSD 990 EVO 1TB", "M.2", 1024),
            ("samsung.980-pro-1tb", "samsung.nvme", "samsung", "Samsung SSD 980 PRO 1TB", "M.2", 1024),
            ("samsung.980-pro-2tb", "samsung.nvme", "samsung", "Samsung SSD 980 PRO 2TB", "M.2", 2048),
            ("samsung.970-evo-1tb", "samsung.nvme", "samsung", "Samsung SSD 970 EVO 1TB", "M.2", 1024),
            ("samsung.970-evo-plus-1tb", "samsung.nvme", "samsung", "Samsung SSD 970 EVO Plus 1TB", "M.2", 1024),
            ("samsung.960-evo-500gb", "samsung.nvme", "samsung", "Samsung SSD 960 EVO 500GB", "M.2", 512),
            // NVMe SSDs - WD
            ("westerndigital.sn850x-1tb", "westerndigital.nvme", "westerndigital", "WD Black SN850X 1TB", "M.2", 1024),
            ("westerndigital.sn850x-2tb", "westerndigital.nvme", "westerndigital", "WD Black SN850X 2TB", "M.2", 2048),
            ("westerndigital.sn770-1tb", "westerndigital.nvme", "westerndigital", "WD Black SN770 1TB", "M.2", 1024),
            ("westerndigital.sn570-1tb", "westerndigital.nvme", "westerndigital", "WD Blue SN570 1TB", "M.2", 1024),
            // NVMe SSDs - SK Hynix
            ("skhynix.platinum-p41-1tb", "skhynix.nvme", "skhynix", "SK Hynix Platinum P41 1TB", "M.2", 1024),
            ("skhynix.platinum-p41-2tb", "skhynix.nvme", "skhynix", "SK Hynix Platinum P41 2TB", "M.2", 2048),
            ("skhynix.gold-p31-1tb", "skhynix.nvme", "skhynix", "SK Hynix Gold P31 1TB", "M.2", 1024),
            // NVMe SSDs - Kingston
            ("kingston.kc3000-1tb", "kingston.nvme", "kingston", "Kingston KC3000 1TB", "M.2", 1024),
            ("kingston.nv2-1tb", "kingston.nvme", "kingston", "Kingston NV2 1TB", "M.2", 1024),
            // NVMe SSDs - Intel
            ("intel.660p-1tb", "intel.nvme", "intel", "Intel SSD 660p 1TB", "M.2", 1024),
            ("intel.670p-1tb", "intel.nvme", "intel", "Intel SSD 670p 1TB", "M.2", 1024),
            ("intel.optane-905p-960gb", "intel.optane", "intel", "Intel Optane SSD 905P 960GB", "HHHL", 960),
            // NVMe SSDs - Corsair
            ("corsair.mp600-1tb", "kingston.nvme", "corsair", "Corsair MP600 1TB", "M.2", 1024),
            ("corsair.mp700-1tb", "kingston.nvme", "corsair", "Corsair MP700 1TB", "M.2", 1024),
            // NVMe SSDs - Crucial
            ("crucial.t705-1tb", "micron.nvme", "crucial", "Crucial T705 1TB", "M.2", 1024),
            ("crucial.t500-1tb", "micron.nvme", "crucial", "Crucial T500 1TB", "M.2", 1024),
            ("crucial.p5-plus-1tb", "micron.nvme", "crucial", "Crucial P5 Plus 1TB", "M.2", 1024),
            // NVMe SSDs - ADATA
            ("adata.s70-1tb", "kingston.nvme", "adata", "ADATA Legend 970 1TB", "M.2", 1024),
            ("adata.s50-1tb", "kingston.nvme", "adata", "ADATA XPG Gammix S50 1TB", "M.2", 1024),
            // SATA SSDs
            ("samsung.870-evo-1tb", "intel.ahci", "samsung", "Samsung SSD 870 EVO 1TB", "2.5\"", 1024),
            ("samsung.870-evo-500gb", "intel.ahci", "samsung", "Samsung SSD 870 EVO 500GB", "2.5\"", 512),
            ("samsung.860-evo-1tb", "intel.ahci", "samsung", "Samsung SSD 860 EVO 1TB", "2.5\"", 1024),
            ("crucial.mx500-1tb", "intel.ahci", "crucial", "Crucial MX500 1TB", "2.5\"", 1024),
            ("crucial.bx500-1tb", "intel.ahci", "crucial", "Crucial BX500 1TB", "2.5\"", 1024),
            ("kingston.a400-960gb", "intel.ahci", "kingston", "Kingston A400 960GB", "2.5\"", 960),
            ("westerndigital.blue-1tb", "intel.ahci", "westerndigital", "WD Blue SA510 1TB", "2.5\"", 1024),
            // SATA HDDs
            ("seagate.barracuda-2tb", "intel.ahci", "seagate", "Seagate Barracuda 2TB (7200RPM)", "3.5\"", 2048),
            ("seagate.barracuda-4tb", "intel.ahci", "seagate", "Seagate Barracuda 4TB (5400RPM)", "3.5\"", 4096),
            ("seagate.firecuda-2tb", "intel.ahci", "seagate", "Seagate FireCuda 2TB (7200RPM)", "3.5\"", 2048),
            ("seagate.ironwolf-4tb", "intel.ahci", "seagate", "Seagate IronWolf 4TB (5900RPM)", "3.5\"", 4096),
            ("westerndigital.black-1tb", "intel.ahci", "westerndigital", "WD Black 1TB (7200RPM)", "3.5\"", 1024),
            ("westerndigital.blue-2tb", "intel.ahci", "westerndigital", "WD Blue 2TB (5400RPM)", "3.5\"", 2048),
            ("westerndigital.red-4tb", "intel.ahci", "westerndigital", "WD Red 4TB (5400RPM)", "3.5\"", 4096),
            ("westerndigital.gold-4tb", "intel.ahci", "westerndigital", "WD Gold 4TB (7200RPM)", "3.5\"", 4096),
            ("toshiba.x300-4tb", "intel.ahci", "toshiba", "Toshiba X300 4TB (7200RPM)", "3.5\"", 4096),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO StorageDevices (Id, ControllerId, ManufacturerId, Name, FormFactor, CapacityGb)
            VALUES ($id, $cid, $mid, $name, $ff, $cap)
            """;

        foreach (var (id, cid, mid, name, ff, cap) in devices)
        {
            if (ct.IsCancellationRequested) break;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$cid", (object?)cid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$mid", mid);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$ff", (object?)ff ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cap", (object?)cap ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            count++;
        }

        // Storage sensor mappings
        count += SeedStorageSensorMappings(conn, ct);

        return count;
    }

    private static int SeedStorageSensorMappings(SqliteConnection conn, CancellationToken ct)
    {
        // Remove mappings existentes para garantir idempotência
        using var clear = conn.CreateCommand();
        clear.CommandText = "DELETE FROM StorageSensorMappings";
        clear.ExecuteNonQuery();

        var count = 0;

        var mappings = new (string Type, string Preferred)[]
        {
            ("Temperature", "Temperature"),
            ("Load", "Total Activity"),
            ("Load", "Read Activity"),
            ("Load", "Write Activity"),
        };

        using var cmd = conn.CreateCommand();
        using var query = conn.CreateCommand();
        query.CommandText = "SELECT Id FROM StorageDevices";

        var ids = new List<string>();
        using (var reader = query.ExecuteReader())
        {
            while (reader.Read())
                ids.Add(reader.GetString(0));
        }

        cmd.CommandText = """
            INSERT OR IGNORE INTO StorageSensorMappings (StorageDeviceId, SensorType, PreferredName, Priority)
            VALUES ($id, $type, $pref, $pri)
            """;

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            var priority = 0;
            foreach (var (type, pref) in mappings)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$id", id);
                cmd.Parameters.AddWithValue("$type", type);
                cmd.Parameters.AddWithValue("$pref", pref);
                cmd.Parameters.AddWithValue("$pri", priority++);
                cmd.ExecuteNonQuery();
                count++;
            }
        }

        return count;
    }

    private static int SeedMotherboards(SqliteConnection conn, CancellationToken ct)
    {
        var count = 0;

        var mobos = new (string Id, string ManId, string Name, string? SuperIo, string? Chipset, string? Factor)[]
        {
            // ASUS
            ("asus.rog-maximus-z790-hero", "asus", "ROG MAXIMUS Z790 HERO", "nuvoton.nct6798d", "Z790", "ATX"),
            ("asus.rog-maximus-z790-dark-hero", "asus", "ROG MAXIMUS Z790 DARK HERO", "nuvoton.nct6798d", "Z790", "ATX"),
            ("asus.rog-maximus-z690-hero", "asus", "ROG MAXIMUS Z690 HERO", "nuvoton.nct6798d", "Z690", "ATX"),
            ("asus.rog-strix-z790-e", "asus", "ROG STRIX Z790-E GAMING", "nuvoton.nct6798d", "Z790", "ATX"),
            ("asus.rog-strix-z790-f", "asus", "ROG STRIX Z790-F GAMING", "nuvoton.nct6798d", "Z790", "ATX"),
            ("asus.rog-strix-b760-f", "asus", "ROG STRIX B760-F GAMING", "nuvoton.nct6798d", "B760", "ATX"),
            ("asus.rog-strix-x670e-e", "asus", "ROG STRIX X670E-E GAMING", "nuvoton.nct6798d", "X670E", "ATX"),
            ("asus.rog-crosshair-x670e-hero", "asus", "ROG CROSSHAIR X670E HERO", "nuvoton.nct6798d", "X670E", "ATX"),
            ("asus.rog-crosshair-x870e-hero", "asus", "ROG CROSSHAIR X870E HERO", "nuvoton.nct6798d", "X870E", "ATX"),
            ("asus.tuf-gaming-z790-plus", "asus", "TUF GAMING Z790-PLUS", "nuvoton.nct6798d", "Z790", "ATX"),
            ("asus.tuf-gaming-b760-plus", "asus", "TUF GAMING B760-PLUS", "nuvoton.nct6798d", "B760", "ATX"),
            ("asus.tuf-gaming-b650-plus", "asus", "TUF GAMING B650-PLUS", "nuvoton.nct6798d", "B650", "ATX"),
            ("asus.tuf-gaming-a620m-plus", "asus", "TUF GAMING A620M-PLUS", "nuvoton.nct6798d", "A620", "mATX"),
            ("asus.prime-z790-p", "asus", "PRIME Z790-P", "nuvoton.nct6796d", "Z790", "ATX"),
            ("asus.prime-b760m-a", "asus", "PRIME B760M-A", "nuvoton.nct6796d", "B760", "mATX"),
            ("asus.proart-z790-creator", "asus", "ProArt Z790-CREATOR", "nuvoton.nct6798d", "Z790", "ATX"),
            ("asus.rog-maximus-xiii-hero", "asus", "ROG MAXIMUS XIII HERO", "nuvoton.nct6795d", "Z590", "ATX"),
            ("asus.rog-maximus-xii-hero", "asus", "ROG MAXIMUS XII HERO", "nuvoton.nct6795d", "Z490", "ATX"),
            ("asus.rog-maximus-xi-hero", "asus", "ROG MAXIMUS XI HERO", "nuvoton.nct6793d", "Z390", "ATX"),
            ("asus.rog-maximus-x-hero", "asus", "ROG MAXIMUS X HERO", "nuvoton.nct6793d", "Z370", "ATX"),
            ("asus.rog-maximus-ix-hero", "asus", "ROG MAXIMUS IX HERO", "nuvoton.nct6793d", "Z270", "ATX"),
            ("asus.rog-strix-x570-e", "asus", "ROG STRIX X570-E GAMING", "nuvoton.nct6795d", "X570", "ATX"),
            ("asus.tuf-gaming-b550m-plus", "asus", "TUF GAMING B550M-PLUS", "nuvoton.nct6795d", "B550", "mATX"),
            ("asus.tuf-gaming-x570-plus", "asus", "TUF GAMING X570-PLUS", "nuvoton.nct6795d", "X570", "ATX"),
            ("asus.prime-x570-p", "asus", "PRIME X570-P", "nuvoton.nct6795d", "X570", "ATX"),
            ("asus.prime-a520m-a", "asus", "PRIME A520M-A", "nuvoton.nct6795d", "A520", "mATX"),
            // Gigabyte
            ("gigabyte.z790-aorus-master", "gigabyte", "Z790 AORUS MASTER", "ite.it8688e", "Z790", "ATX"),
            ("gigabyte.z790-aorus-elite", "gigabyte", "Z790 AORUS ELITE", "ite.it8688e", "Z790", "ATX"),
            ("gigabyte.z790-ud", "gigabyte", "Z790 UD", "ite.it8688e", "Z790", "ATX"),
            ("gigabyte.z690-aorus-pro", "gigabyte", "Z690 AORUS PRO", "ite.it8688e", "Z690", "ATX"),
            ("gigabyte.x670e-aorus-master", "gigabyte", "X670E AORUS MASTER", "ite.it8688e", "X670E", "ATX"),
            ("gigabyte.x670e-aorus-pro", "gigabyte", "X670E AORUS PRO", "ite.it8688e", "X670E", "ATX"),
            ("gigabyte.b650-aorus-master", "gigabyte", "B650 AORUS MASTER", "ite.it8688e", "B650", "ATX"),
            ("gigabyte.b650-aorus-elite", "gigabyte", "B650 AORUS ELITE", "ite.it8688e", "B650", "ATX"),
            ("gigabyte.b760-aorus-master", "gigabyte", "B760 AORUS MASTER", "ite.it8688e", "B760", "ATX"),
            ("gigabyte.b760-aorus-elite", "gigabyte", "B760 AORUS ELITE", "ite.it8688e", "B760", "ATX"),
            ("gigabyte.x570-aorus-master", "gigabyte", "X570 AORUS MASTER", "ite.it8688e", "X570", "ATX"),
            ("gigabyte.x570-aorus-pro", "gigabyte", "X570 AORUS PRO", "ite.it8688e", "X570", "ATX"),
            ("gigabyte.b550-aorus-master", "gigabyte", "B550 AORUS MASTER", "ite.it8688e", "B550", "ATX"),
            ("gigabyte.b550-aorus-pro", "gigabyte", "B550 AORUS PRO", "ite.it8688e", "B550", "ATX"),
            ("gigabyte.ga-b450-aorus-pro", "gigabyte", "GA-B450 AORUS PRO", "ite.it8686e", "B450", "ATX"),
            ("gigabyte.z390-aorus-pro", "gigabyte", "Z390 AORUS PRO", "ite.it8686e", "Z390", "ATX"),
            // MSI
            ("msi.meg-z790-godlike", "msi", "MEG Z790 GODLIKE", "nuvoton.nct6798d", "Z790", "E-ATX"),
            ("msi.meg-z790-ace", "msi", "MEG Z790 ACE", "nuvoton.nct6798d", "Z790", "ATX"),
            ("msi.mpg-z790-carbon", "msi", "MPG Z790 CARBON WIFI", "nuvoton.nct6798d", "Z790", "ATX"),
            ("msi.mpg-z790-edge", "msi", "MPG Z790 EDGE WIFI", "nuvoton.nct6798d", "Z790", "ATX"),
            ("msi.pro-z790-a", "msi", "PRO Z790-A WIFI", "nuvoton.nct6796d", "Z790", "ATX"),
            ("msi.mag-z790-tomahawk", "msi", "MAG Z790 TOMAHAWK", "nuvoton.nct6798d", "Z790", "ATX"),
            ("msi.meg-x670e-ace", "msi", "MEG X670E ACE", "nuvoton.nct6798d", "X670E", "ATX"),
            ("msi.mpg-x670e-carbon", "msi", "MPG X670E CARBON", "nuvoton.nct6798d", "X670E", "ATX"),
            ("msi.mag-x670e-tomahawk", "msi", "MAG X670E TOMAHAWK", "nuvoton.nct6798d", "X670E", "ATX"),
            ("msi.mpg-b650-carbon", "msi", "MPG B650 CARBON", "nuvoton.nct6798d", "B650", "ATX"),
            ("msi.mag-b650-tomahawk", "msi", "MAG B650 TOMAHAWK", "nuvoton.nct6798d", "B650", "ATX"),
            ("msi.pro-b650m-a", "msi", "PRO B650M-A WIFI", "nuvoton.nct6796d", "B650", "mATX"),
            ("msi.meg-z690-godlike", "msi", "MEG Z690 GODLIKE", "nuvoton.nct6798d", "Z690", "E-ATX"),
            ("msi.mpg-z690-carbon", "msi", "MPG Z690 CARBON", "nuvoton.nct6798d", "Z690", "ATX"),
            ("msi.mag-z690-tomahawk", "msi", "MAG Z690 TOMAHAWK", "nuvoton.nct6798d", "Z690", "ATX"),
            ("msi.mpg-x570-carbon", "msi", "MPG X570 CARBON", "nuvoton.nct6795d", "X570", "ATX"),
            ("msi.mag-x570-tomahawk", "msi", "MAG X570 TOMAHAWK", "nuvoton.nct6795d", "X570", "ATX"),
            ("msi.b550-a-pro", "msi", "B550-A PRO", "nuvoton.nct6795d", "B550", "ATX"),
            ("msi.b450-tomahawk", "msi", "B450 TOMAHAWK MAX", "nuvoton.nct6793d", "B450", "ATX"),
            ("msi.z390-a-pro", "msi", "Z390-A PRO", "nuvoton.nct6793d", "Z390", "ATX"),
            // ASRock
            ("asrock.z790-taichi", "asrock", "Z790 Taichi", "nuvoton.nct6798d", "Z790", "ATX"),
            ("asrock.z790-steel-legend", "asrock", "Z790 Steel Legend", "nuvoton.nct6798d", "Z790", "ATX"),
            ("asrock.z790-pro-rs", "asrock", "Z790 PRO RS", "nuvoton.nct6796d", "Z790", "ATX"),
            ("asrock.x670e-taichi", "asrock", "X670E Taichi", "nuvoton.nct6798d", "X670E", "ATX"),
            ("asrock.b650e-taichi", "asrock", "B650E Taichi", "nuvoton.nct6798d", "B650E", "ATX"),
            ("asrock.b650-pro-rs", "asrock", "B650 PRO RS", "nuvoton.nct6796d", "B650", "ATX"),
            ("asrock.b550-taichi", "asrock", "B550 Taichi", "nuvoton.nct6795d", "B550", "ATX"),
            ("asrock.b550-steel-legend", "asrock", "B550 Steel Legend", "nuvoton.nct6795d", "B550", "ATX"),
            ("asrock.x570-taichi", "asrock", "X570 Taichi", "nuvoton.nct6795d", "X570", "ATX"),
            ("asrock.b450-steel-legend", "asrock", "B450 Steel Legend", "nuvoton.nct6793d", "B450", "ATX"),
            ("asrock.b450m-pro4", "asrock", "B450M PRO4", "nuvoton.nct6793d", "B450", "mATX"),
            // EVGA
            ("evga.z790-dark", "evga", "Z790 DARK KINGPIN", "nuvoton.nct6798d", "Z790", "E-ATX"),
            ("evga.z690-classified", "evga", "Z690 CLASSIFIED", "nuvoton.nct6798d", "Z690", "ATX"),
            ("evga.z590-dark", "evga", "Z590 DARK", "nuvoton.nct6795d", "Z590", "E-ATX"),
            ("evga.x570-dark", "evga", "X570 DARK", "nuvoton.nct6795d", "X570", "ATX"),
            // Supermicro
            ("supermicro.x13sem-f", "supermicro", "X13SEM-F", "nuvoton.nct5104", "W680", "mATX"),
            ("supermicro.x13sae-f", "supermicro", "X13SAE-F", "nuvoton.nct5104", "W680", "ATX"),
            ("supermicro.x12spa-t", "supermicro", "X12SPA-T", "nuvoton.nct5104", "C621A", "ATX"),
            ("supermicro.x11dpi-n", "supermicro", "X11DPI-N", "nuvoton.nct5104", "C621", "ATX"),
            // OEM (Lenovo, Dell, HP)
            ("lenovo.thinkcentre-m75q", "lenovo", "ThinkCentre M75q Gen 2", null, "A520", "SFF"),
            ("lenovo.thinkstation-p360", "lenovo", "ThinkStation P360 Tower", "nuvoton.nct6796d", "W680", "Tower"),
            ("lenovo.thinkpad-x1-carbon-gen11", "lenovo", "ThinkPad X1 Carbon Gen 11", null, null, "Notebook"),
            ("dell.optiplex-7000", "dell", "OptiPlex 7000 Tower", null, "Q670", "Tower"),
            ("dell.precision-3660", "dell", "Precision 3660 Tower", null, "W680", "Tower"),
            ("dell.xps-15-9530", "dell", "XPS 15 9530", null, null, "Notebook"),
            ("hp.spectre-x360-16", "hp", "Spectre x360 16", null, null, "Notebook"),
            ("hp.omen-45l", "hp", "OMEN 45L", null, "Z790", "Tower"),
            ("hp.z4-g5", "hp", "Z4 G5 Workstation", null, "W790", "Tower"),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO Motherboards (Id, ManufacturerId, Name, SuperIoId, Chipset, FormFactor)
            VALUES ($id, $mid, $name, $sid, $chip, $ff)
            """;

        foreach (var (id, mid, name, sid, chip, ff) in mobos)
        {
            if (ct.IsCancellationRequested) break;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$mid", mid);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$sid", (object?)sid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$chip", (object?)chip ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ff", (object?)ff ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            count++;
        }

        return count;
    }

    private static int SeedKnownIssues(SqliteConnection conn, CancellationToken ct)
    {
        var count = 0;

        using var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM KnownIssues";
        del.ExecuteNonQuery();

        var issues = new (string TargetType, string TargetId, string Description, string? Workaround)[]
        {
            ("GpuModel", "nvidia.gpu.rtx-5090", "GPU hotspots podem atingir 105°C em cargas extremas", "Ajustar curvas de fan ou reduzir power limit via MSI Afterburner"),
            ("GpuModel", "nvidia.gpu.rtx-4090", "Conector 12VHPWR pode derreter se mal encaixado", "Verificar conexão completa do conector; usar cabo nativo Seasonic/Corsair"),
            ("GpuModel", "intel.gpu.arc-a770", "Desempenho em DX11 abaixo do esperado sem ReBAR ativo", "Ativar Resizable BAR na BIOS"),
            ("CpuModel", "amd.cpu.ryzen-7-9800x3d", "Temperatura pode oscilar rapidamente (vácuo térmico)", "Ignorar picos; usar curvas de fan suaves"),
            ("CpuModel", "intel.cpu.core-i9-13900k", "Instabilidade em altas frequências com cargas AVX pesadas", "Reduzir offset de voltagem ou limitar Turbo Boost 3.0"),
            ("CpuModel", "intel.cpu.core-i9-14900k", "Oxidação via (Vmin Shift) relatada em altas voltagens", "Manter BIOS atualizada com Intel Baseline Profile"),
            ("CpuArchitecture", "intel.cpu.raptor-lake", "E-core e P-core scheduling no Windows 10 pode ser subótimo", "Usar Windows 11 ou ferramentas de afinidade"),
            ("CpuArchitecture", "amd.cpu.zen-5", "Memória DDR5 acima de 6000MT/s instável em dual-rank", "Usar 2-DIMM single-rank ou limitar para 6000"),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO KnownIssues (TargetType, TargetId, Description, Workaround)
            VALUES ($tt, $tid, $desc, $work)
            """;

        foreach (var (tt, tid, desc, work) in issues)
        {
            if (ct.IsCancellationRequested) break;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$tt", tt);
            cmd.Parameters.AddWithValue("$tid", tid);
            cmd.Parameters.AddWithValue("$desc", desc);
            cmd.Parameters.AddWithValue("$work", (object?)work ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            count++;
        }

        return count;
    }
}
