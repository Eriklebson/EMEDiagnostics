using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using EME.HardwareDatabase.Seed;
using EME.HardwareDatabase.Shared;

namespace EME.HardwareDatabase.Services;

public sealed class HardwareDatabaseUpdateService
{
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly DatabaseVersionService _versionService;
    private readonly DatabaseMigrationService _migrationService;

    public HardwareDatabaseUpdateService()
    {
        _connectionFactory = new DatabaseConnectionFactory();
        _versionService = new DatabaseVersionService(_connectionFactory);
        _migrationService = new DatabaseMigrationService(_connectionFactory, _versionService);
    }

    internal HardwareDatabaseUpdateService(
        DatabaseConnectionFactory connectionFactory,
        DatabaseVersionService versionService,
        DatabaseMigrationService migrationService)
    {
        _connectionFactory = connectionFactory;
        _versionService = versionService;
        _migrationService = migrationService;
    }

    public void EnsureHardwareDatabase()
    {
        try
        {
            using var mutex = new Mutex(false, Constants.MutexName);
            var owned = mutex.WaitOne(TimeSpan.FromSeconds(10));
            if (!owned)
                throw new TimeoutException("Não foi possível obter o mutex do banco de hardware dentro de 10s. Outro processo pode estar atualizando.");

            try
            {
                DatabaseConnectionFactory.EnsureDirectoryExists();
#pragma warning disable CA1416
                EnsurePermissions();
#pragma warning restore CA1416

                var dbPath = Constants.DatabasePath;
                var isNew = !File.Exists(dbPath);

                var initializer = new DatabaseInitializer(_connectionFactory);
                initializer.Initialize();

                if (!isNew)
                {
                    var meta = _versionService.ReadMetadata();
                    if (meta != null)
                    {
                        if (!_versionService.IsSchemaCompatible(1))
                            throw new InvalidOperationException(
                                $"Banco de dados requer schema mais recente. " +
                                $"Atualize o aplicativo para a versão mais recente.");

                        if (_versionService.NeedsSchemaUpdate(Constants.SchemaVersion) ||
                            _versionService.NeedsDataUpdate(Constants.DataVersion))
                        {
                            Backup();
                            _migrationService.ApplyMigrations(Constants.SchemaVersion, Constants.DataVersion);
                        }
                    }
                }

                ValidateIntegrity();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        catch (Exception ex)
        {
            LogError($"Falha no EnsureHardwareDatabase: {ex.Message}");
            throw;
        }
    }

    public void Backup()
    {
        var dbPath = Constants.DatabasePath;
        if (!File.Exists(dbPath)) return;

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupPath = Path.Combine(Constants.BackupDirectory, $"eme-hardware_{timestamp}.db");
        File.Copy(dbPath, backupPath, true);

        var logPath = Path.Combine(Constants.LogsDirectory, $"backup_{timestamp}.log");
        File.WriteAllText(logPath, $"Backup criado em {DateTime.UtcNow:O} de {dbPath} para {backupPath}");

        CleanOldBackups();
    }

    private static void CleanOldBackups()
    {
        try
        {
            var backups = Directory.GetFiles(Constants.BackupDirectory, "eme-hardware_*.db")
                .OrderByDescending(f => f)
                .Skip(5)
                .ToArray();
            foreach (var old in backups) File.Delete(old);
        }
        catch { }
    }

    public async Task<DataSeedSummary?> SeedIfEmptyAsync(CancellationToken ct = default)
    {
        var seeder = new DataSeederCoordinator(_connectionFactory, _versionService);
        return await seeder.SeedIfEmptyAsync(ct);
    }

    public void ValidateIntegrity()
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check";
        var result = cmd.ExecuteScalar()?.ToString();
        if (result != "ok")
            throw new InvalidDataException($"Falha na integridade do banco: {result}");
    }

    [SupportedOSPlatform("windows")]
    private static void EnsurePermissions()
    {
        try
        {
            var di = new DirectoryInfo(Constants.DatabaseFolder);
            if (!di.Exists)
            {
                di.Create();
                var acl = di.GetAccessControl();
                var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                acl.AddAccessRule(new FileSystemAccessRule(
                    sid, FileSystemRights.Modify | FileSystemRights.ReadAndExecute | FileSystemRights.ListDirectory,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
                di.SetAccessControl(acl);
            }
        }
        catch (Exception ex)
        {
            LogError($"Não foi possível ajustar permissões: {ex.Message}");
        }
    }

    private static void LogError(string message)
    {
        try
        {
            Directory.CreateDirectory(Constants.LogsDirectory);
            var logPath = Path.Combine(Constants.LogsDirectory, "errors.log");
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
