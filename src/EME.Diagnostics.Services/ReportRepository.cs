using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;
using Microsoft.Data.Sqlite;

namespace EME.Diagnostics.Services;

public sealed class ReportRepository : IReportRepository, IDisposable
{
    private readonly SqliteConnection _connection;

    public ReportRepository()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EMEDiagnostics", "reports.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connection = new SqliteConnection($"Data Source={dbPath}");
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _connection.OpenAsync(ct);
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Reports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CreatedAt TEXT NOT NULL,
                TestType TEXT NOT NULL,
                Duration TEXT NOT NULL,
                DurationSeconds REAL NOT NULL,
                Status TEXT NOT NULL,
                CpuName TEXT,
                GpuName TEXT,
                MemoryTotalGb REAL NOT NULL DEFAULT 0,
                StorageName TEXT
            );
            CREATE TABLE IF NOT EXISTS ReportEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ReportId INTEGER NOT NULL,
                Component TEXT NOT NULL,
                SensorName TEXT NOT NULL,
                Unit TEXT NOT NULL,
                MinValue REAL,
                MaxValue REAL,
                AvgValue REAL,
                FOREIGN KEY (ReportId) REFERENCES Reports(Id) ON DELETE CASCADE
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> SaveReportAsync(StressReportDetail report, CancellationToken ct = default)
    {
        using var tx = _connection.BeginTransaction();
        try
        {
            using var insertReport = _connection.CreateCommand();
            insertReport.CommandText = """
                INSERT INTO Reports (CreatedAt, TestType, Duration, DurationSeconds, Status, CpuName, GpuName, MemoryTotalGb, StorageName)
                VALUES ($created, $type, $duration, $durationSec, $status, $cpu, $gpu, $mem, $storage);
                SELECT last_insert_rowid();
                """;
            insertReport.Parameters.AddWithValue("$created", report.CreatedAt.ToString("O"));
            insertReport.Parameters.AddWithValue("$type", report.TestType.ToString());
            insertReport.Parameters.AddWithValue("$duration", report.Duration.ToString());
            insertReport.Parameters.AddWithValue("$durationSec", report.Duration.TotalSeconds);
            insertReport.Parameters.AddWithValue("$status", report.Status);
            insertReport.Parameters.AddWithValue("$cpu", (object?)report.CpuName ?? DBNull.Value);
            insertReport.Parameters.AddWithValue("$gpu", (object?)report.GpuName ?? DBNull.Value);
            insertReport.Parameters.AddWithValue("$mem", report.MemoryTotalGb);
            insertReport.Parameters.AddWithValue("$storage", (object?)report.StorageName ?? DBNull.Value);

            var reportId = (long)(await insertReport.ExecuteScalarAsync(ct).ConfigureAwait(false))!;

            foreach (var entry in report.Entries)
            {
                using var insertEntry = _connection.CreateCommand();
                insertEntry.CommandText = """
                    INSERT INTO ReportEntries (ReportId, Component, SensorName, Unit, MinValue, MaxValue, AvgValue)
                    VALUES ($rid, $comp, $sensor, $unit, $min, $max, $avg);
                    """;
                insertEntry.Parameters.AddWithValue("$rid", reportId);
                insertEntry.Parameters.AddWithValue("$comp", entry.Component);
                insertEntry.Parameters.AddWithValue("$sensor", entry.SensorName);
                insertEntry.Parameters.AddWithValue("$unit", entry.Unit);
                insertEntry.Parameters.AddWithValue("$min", (object?)entry.MinValue ?? DBNull.Value);
                insertEntry.Parameters.AddWithValue("$max", (object?)entry.MaxValue ?? DBNull.Value);
                insertEntry.Parameters.AddWithValue("$avg", (object?)entry.AvgValue ?? DBNull.Value);
                await insertEntry.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            tx.Commit();
            return reportId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<List<StressReportSummary>> GetAllReportsAsync(CancellationToken ct = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT r.Id, r.CreatedAt, r.TestType, r.Duration, r.DurationSeconds, r.Status,
                   (SELECT COUNT(*) FROM ReportEntries e WHERE e.ReportId = r.Id) AS EntryCount
            FROM Reports r ORDER BY r.Id DESC;
            """;

        var results = new List<StressReportSummary>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new StressReportSummary(
                reader.GetInt64(0),
                DateTime.Parse(reader.GetString(1)),
                Enum.Parse<ReportTestType>(reader.GetString(2)),
                TimeSpan.Parse(reader.GetString(3)),
                reader.GetString(5),
                reader.GetInt32(6)));
        }
        return results;
    }

    public async Task<StressReportDetail?> GetReportAsync(long id, CancellationToken ct = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Reports WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

        var report = new StressReportDetail(
            reader.GetInt64(0),
            DateTime.Parse(reader.GetString(1)),
            Enum.Parse<ReportTestType>(reader.GetString(2)),
            TimeSpan.Parse(reader.GetString(3)),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetDouble(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            Array.Empty<ReportEntry>());

        using var entryCmd = _connection.CreateCommand();
        entryCmd.CommandText = "SELECT Component, SensorName, Unit, MinValue, MaxValue, AvgValue FROM ReportEntries WHERE ReportId = $rid ORDER BY Id;";
        entryCmd.Parameters.AddWithValue("$rid", id);

        var entries = new List<ReportEntry>();
        using var entryReader = await entryCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await entryReader.ReadAsync(ct).ConfigureAwait(false))
        {
            entries.Add(new ReportEntry(
                entryReader.GetString(0),
                entryReader.GetString(1),
                entryReader.GetString(2),
                entryReader.IsDBNull(3) ? null : entryReader.GetDouble(3),
                entryReader.IsDBNull(4) ? null : entryReader.GetDouble(4),
                entryReader.IsDBNull(5) ? null : entryReader.GetDouble(5)));
        }

        return report with { Entries = entries.AsReadOnly() };
    }

    public async Task DeleteReportAsync(long id, CancellationToken ct = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ReportEntries WHERE ReportId = $id; DELETE FROM Reports WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public void Dispose() => _connection.Dispose();
}
