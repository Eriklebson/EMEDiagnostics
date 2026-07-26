namespace EME.HardwareDatabase.Shared;

public static class Constants
{
    public const string DatabaseFolder = @"C:\ProgramData\EME\HardwareDatabase";
    public const string DatabaseFile = "eme-hardware.db";
    public const string BackupFolder = "backups";
    public const string LogsFolder = "logs";
    public const string MutexName = @"Global\EME_HARDWARE_DATABASE_UPDATE";
    public const int DefaultTimeoutSeconds = 30;
    public const string SchemaVersion = "1.0.0";
    public const string DataVersion = "2026.07.001";
    public const string MinimumCoreVersion = "2.26.0";
    public const string MinimumDiagnosticsVersion = "1.0.0";

    public static string DatabasePath => Path.Combine(DatabaseFolder, DatabaseFile);
    public static string BackupDirectory => Path.Combine(DatabaseFolder, BackupFolder);
    public static string LogsDirectory => Path.Combine(DatabaseFolder, LogsFolder);
}
