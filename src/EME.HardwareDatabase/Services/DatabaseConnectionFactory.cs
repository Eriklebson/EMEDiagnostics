using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Shared;

namespace EME.HardwareDatabase.Services;

public sealed class DatabaseConnectionFactory
{
    private readonly string _connectionString;

    public DatabaseConnectionFactory()
    {
        var dbPath = Constants.DatabasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = Constants.DefaultTimeoutSeconds
        }.ToString();
    }

    public SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=30000;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public SqliteConnection CreateReadOnlyConnection()
    {
        var dbPath = Constants.DatabasePath;
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = Constants.DefaultTimeoutSeconds
        }.ToString();
        var conn = new SqliteConnection(cs);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=30000;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public static void EnsureDirectoryExists()
    {
        Directory.CreateDirectory(Constants.DatabaseFolder);
        Directory.CreateDirectory(Constants.BackupDirectory);
        Directory.CreateDirectory(Constants.LogsDirectory);
    }
}
