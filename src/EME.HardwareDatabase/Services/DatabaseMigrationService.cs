using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Shared;

namespace EME.HardwareDatabase.Services;

public sealed class DatabaseMigrationService
{
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly DatabaseVersionService _versionService;

    public DatabaseMigrationService(DatabaseConnectionFactory connectionFactory, DatabaseVersionService versionService)
    {
        _connectionFactory = connectionFactory;
        _versionService = versionService;
    }

    public void ApplyMigrations(string targetSchemaVersion, string targetDataVersion)
    {
        if (_versionService.IsDowngrade(targetSchemaVersion, true))
            throw new InvalidOperationException($"Downgrade de schema bloqueado: banco atual é mais recente que {targetSchemaVersion}");

        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();

        try
        {
            var applied = GetAppliedMigrations(conn);
            var pending = GetPendingMigrations(applied);

            foreach (var migration in pending)
            {
                ExecuteMigration(conn, migration);
                LogMigration(conn, migration);
            }

            _versionService.UpdateMetadata(targetSchemaVersion, targetDataVersion);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static HashSet<string> GetAppliedMigrations(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Version FROM DatabaseMigrations WHERE Success = 1";
        var result = new HashSet<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    private static List<MigrationStep> GetPendingMigrations(HashSet<string> applied)
    {
        return AllMigrations.Where(m => !applied.Contains(m.Version)).ToList();
    }

    private static void ExecuteMigration(SqliteConnection conn, MigrationStep migration)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = migration.Sql;
        cmd.ExecuteNonQuery();
    }

    private static void LogMigration(SqliteConnection conn, MigrationStep migration)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO DatabaseMigrations (Version, Description, AppliedAt, Success) VALUES ($v, $d, $now, 1)";
        cmd.Parameters.AddWithValue("$v", migration.Version);
        cmd.Parameters.AddWithValue("$d", migration.Description);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow);
        cmd.ExecuteNonQuery();
    }

    private static readonly List<MigrationStep> AllMigrations = new()
    {
        new MigrationStep("1.0.0", "Criação inicial do banco", "-- Schema created by DatabaseInitializer")
    };
}

internal sealed record MigrationStep(string Version, string Description, string Sql);
