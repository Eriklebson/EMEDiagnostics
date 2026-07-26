using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Shared;

namespace EME.HardwareDatabase.Services;

public sealed class DatabaseInitializer
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public DatabaseInitializer(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();

        CreateTables(conn);
        InsertInitialMetadata(conn);

        tx.Commit();
    }

    private static void CreateTables(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS DatabaseMetadata (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                SchemaVersion TEXT NOT NULL,
                DataVersion TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                MinimumCoreVersion TEXT,
                MinimumDiagnosticsVersion TEXT,
                Checksum TEXT
            );

            CREATE TABLE IF NOT EXISTS Manufacturers (
                Id TEXT PRIMARY KEY,
                DisplayName TEXT NOT NULL,
                ShortName TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CpuArchitectures (
                Id TEXT PRIMARY KEY,
                ManufacturerId TEXT NOT NULL REFERENCES Manufacturers(Id),
                Name TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Segment TEXT NOT NULL,
                Released INTEGER
            );

            CREATE TABLE IF NOT EXISTS CpuFamilies (
                Id TEXT PRIMARY KEY,
                ArchitectureId TEXT NOT NULL REFERENCES CpuArchitectures(Id),
                Name TEXT NOT NULL,
                DisplayName TEXT
            );

            CREATE TABLE IF NOT EXISTS CpuModels (
                Id TEXT PRIMARY KEY,
                FamilyId TEXT NOT NULL REFERENCES CpuFamilies(Id),
                Name TEXT NOT NULL,
                Cores INTEGER,
                Threads INTEGER,
                BaseClock REAL,
                BoostClock REAL,
                Tdp INTEGER
            );

            CREATE TABLE IF NOT EXISTS CpuSensorMappings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CpuModelId TEXT REFERENCES CpuModels(Id),
                CpuArchitectureId TEXT REFERENCES CpuArchitectures(Id),
                SensorType TEXT NOT NULL,
                PreferredName TEXT NOT NULL,
                Priority INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Motherboards (
                Id TEXT PRIMARY KEY,
                ManufacturerId TEXT NOT NULL REFERENCES Manufacturers(Id),
                Name TEXT NOT NULL,
                SuperIoId TEXT REFERENCES SuperIoChips(Id),
                Chipset TEXT,
                FormFactor TEXT
            );

            CREATE TABLE IF NOT EXISTS SuperIoChips (
                Id TEXT PRIMARY KEY,
                ManufacturerId TEXT NOT NULL REFERENCES Manufacturers(Id),
                Name TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS MotherboardFanMappings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MotherboardId TEXT NOT NULL REFERENCES Motherboards(Id),
                RawName TEXT NOT NULL,
                MappedName TEXT NOT NULL,
                Category TEXT NOT NULL DEFAULT 'Motherboard'
            );

            CREATE TABLE IF NOT EXISTS MotherboardTemperatureMappings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MotherboardId TEXT NOT NULL REFERENCES Motherboards(Id),
                RawName TEXT NOT NULL,
                MappedName TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS MotherboardVoltageMappings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MotherboardId TEXT NOT NULL REFERENCES Motherboards(Id),
                RawName TEXT NOT NULL,
                MappedName TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS GpuArchitectures (
                Id TEXT PRIMARY KEY,
                ManufacturerId TEXT NOT NULL REFERENCES Manufacturers(Id),
                Name TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Segment TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS GpuModels (
                Id TEXT PRIMARY KEY,
                ArchitectureId TEXT NOT NULL REFERENCES GpuArchitectures(Id),
                Name TEXT NOT NULL,
                VramMb INTEGER,
                VramType TEXT
            );

            CREATE TABLE IF NOT EXISTS GpuSensorMappings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GpuModelId TEXT REFERENCES GpuModels(Id),
                GpuArchitectureId TEXT REFERENCES GpuArchitectures(Id),
                SensorType TEXT NOT NULL,
                PreferredName TEXT NOT NULL,
                Priority INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS MemoryStandards (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                MemoryType TEXT NOT NULL,
                MaxSpeedMhz INTEGER
            );

            CREATE TABLE IF NOT EXISTS MemoryModels (
                Id TEXT PRIMARY KEY,
                StandardId TEXT NOT NULL REFERENCES MemoryStandards(Id),
                ManufacturerId TEXT NOT NULL REFERENCES Manufacturers(Id),
                PartNumber TEXT NOT NULL,
                CapacityMb INTEGER,
                SpeedMhz INTEGER,
                FormFactor TEXT,
                Ecc INTEGER DEFAULT 0,
                Registered INTEGER DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS StorageControllers (
                Id TEXT PRIMARY KEY,
                ManufacturerId TEXT NOT NULL REFERENCES Manufacturers(Id),
                Name TEXT NOT NULL,
                Interface TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS StorageDevices (
                Id TEXT PRIMARY KEY,
                ControllerId TEXT REFERENCES StorageControllers(Id),
                ManufacturerId TEXT NOT NULL REFERENCES Manufacturers(Id),
                Name TEXT NOT NULL,
                FormFactor TEXT,
                CapacityGb INTEGER
            );

            CREATE TABLE IF NOT EXISTS StorageSensorMappings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StorageDeviceId TEXT REFERENCES StorageDevices(Id),
                SensorType TEXT NOT NULL,
                PreferredName TEXT NOT NULL,
                Priority INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS HardwareAliases (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TargetType TEXT NOT NULL,
                TargetId TEXT NOT NULL,
                Alias TEXT NOT NULL,
                MatchMode TEXT NOT NULL DEFAULT 'Substring'
            );

            CREATE UNIQUE INDEX IF NOT EXISTS IX_HardwareAliases_Alias ON HardwareAliases(Alias);

            CREATE TABLE IF NOT EXISTS PowerSupplies (
                Id TEXT PRIMARY KEY,
                ManufacturerId TEXT NOT NULL REFERENCES Manufacturers(Id),
                Name TEXT NOT NULL,
                Type TEXT NOT NULL,
                Wattage INTEGER,
                Efficiency TEXT,
                Modular TEXT
            );

            CREATE TABLE IF NOT EXISTS PsuSensorMappings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PowerSupplyId TEXT REFERENCES PowerSupplies(Id),
                SensorType TEXT NOT NULL,
                PreferredName TEXT NOT NULL,
                Priority INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS NetworkDevices (
                Id TEXT PRIMARY KEY,
                ManufacturerId TEXT NOT NULL REFERENCES Manufacturers(Id),
                Name TEXT NOT NULL,
                Interface TEXT,
                WirelessStandard TEXT,
                DeviceType TEXT NOT NULL DEFAULT 'Wired'
            );

            CREATE TABLE IF NOT EXISTS NetworkSensorMappings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                NetworkDeviceId TEXT REFERENCES NetworkDevices(Id),
                SensorType TEXT NOT NULL,
                PreferredName TEXT NOT NULL,
                Priority INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS KnownIssues (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TargetType TEXT NOT NULL,
                TargetId TEXT NOT NULL,
                Description TEXT NOT NULL,
                Workaround TEXT,
                AffectedFrom TEXT,
                AffectedTo TEXT
            );
            
            CREATE TABLE IF NOT EXISTS DatabaseMigrations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Version TEXT NOT NULL,
                Description TEXT NOT NULL,
                AppliedAt TEXT NOT NULL,
                Checksum TEXT,
                Success INTEGER NOT NULL DEFAULT 1
            );
        ";
        cmd.ExecuteNonQuery();
    }

    private static void InsertInitialMetadata(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO DatabaseMetadata (Id, SchemaVersion, DataVersion, CreatedAt, UpdatedAt, " +
                          "MinimumCoreVersion, MinimumDiagnosticsVersion) VALUES " +
                          "(1, $sv, $dv, $now, $now, $mcv, $mdv)";
        cmd.Parameters.AddWithValue("$sv", Constants.SchemaVersion);
        cmd.Parameters.AddWithValue("$dv", Constants.DataVersion);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("$mcv", Constants.MinimumCoreVersion);
        cmd.Parameters.AddWithValue("$mdv", Constants.MinimumDiagnosticsVersion);
        cmd.ExecuteNonQuery();
    }
}
