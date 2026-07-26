using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Services;

namespace EME.HardwareDatabase.Seed;

public sealed class TechApiCpuImporter : ISeedImporter
{
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly HttpClient _http;
    private const string ApiBase = "https://gettechapi.github.io/TechAPI";

    public string SourceName => "TechAPI CPU Database";

    public TechApiCpuImporter(DatabaseConnectionFactory connectionFactory, HttpClient? http = null)
    {
        _connectionFactory = connectionFactory;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "EME.HardwareDatabase/1.0");
    }

    public async Task<SeedResult> ImportAsync(CancellationToken ct = default)
    {
        var errors = new ConcurrentBag<string>();
        var totalErrors = 0;
        var totalImported = 0;

        try
        {
            Debug.WriteLine("Obtendo lista de CPUs do TechAPI...");
            var slugs = await GetAllCpuSlugsAsync(ct);

            if (slugs.Count == 0)
            {
                errors.Add("Nenhuma CPU encontrada no TechAPI");
                return new SeedResult(SourceName, 0, 1, errors.ToList().AsReadOnly());
            }

            Debug.WriteLine($"Encontradas {slugs.Count} CPUs no TechAPI");

            var semaphore = new SemaphoreSlim(10);
            var batchLock = new object();
            var conn = _connectionFactory.CreateConnection();
            var tx = conn.BeginTransaction();
            var counter = 0;
            var archCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var manCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var familyCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 10,
                    CancellationToken = ct
                };

                await Parallel.ForEachAsync(slugs, parallelOptions, async (slug, token) =>
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        var detailUrl = $"{ApiBase}/v1/cpus/{slug}/index.json";
                        var response = await _http.GetAsync(detailUrl, token);
                        if (!response.IsSuccessStatusCode)
                        {
                            Interlocked.Increment(ref totalErrors);
                            errors.Add($"HTTP {(int)response.StatusCode} para {slug}");
                            return;
                        }

                        var content = await response.Content.ReadAsStringAsync(token);
                        var apiCpu = JsonSerializer.Deserialize<TechApiCpuResponse>(content);
                        if (apiCpu == null) return;

                        lock (batchLock)
                        {
                            ImportSingleCpu(conn, apiCpu, archCache, manCache, familyCache);
                            counter++;
                            totalImported++;

                            if (counter % 100 == 0)
                            {
                                tx.Commit();
                                tx.Dispose();
                                tx = conn.BeginTransaction();
                                Debug.WriteLine($"  Processadas {counter}/{slugs.Count} CPUs...");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref totalErrors);
                        errors.Add($"Erro em {slug}: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
            finally
            {
                tx.Dispose();
                conn.Dispose();
            }
        }
        catch (Exception ex)
        {
            totalErrors++;
            errors.Add($"Falha geral no importador TechAPI CPU: {ex.Message}");
        }

        return new SeedResult(SourceName, totalImported, totalErrors,
            errors.IsEmpty ? null : errors.ToList().AsReadOnly());
    }

    private async Task<List<string>> GetAllCpuSlugsAsync(CancellationToken ct)
    {
        var slugs = new List<string>();
        var url = $"{ApiBase}/v1/cpus/";

        while (url != null)
        {
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(ct);
            var page = JsonSerializer.Deserialize<CpuListPage>(content);
            if (page?.Results == null) break;

            foreach (var r in page.Results)
            {
                if (!string.IsNullOrEmpty(r.Slug))
                    slugs.Add(r.Slug);
            }

            url = page.Next;
        }

        return slugs;
    }

    private void ImportSingleCpu(SqliteConnection conn, TechApiCpuResponse cpu,
        ConcurrentDictionary<string, string> archCache,
        ConcurrentDictionary<string, string> manCache,
        ConcurrentDictionary<string, string> familyCache)
    {
        var manufacturer = (cpu.Manufacturer?.Slug ?? "unknown").ToLowerInvariant();
        var displayName = cpu.Manufacturer?.Name ?? manufacturer;

        EnsureManufacturer(conn, manufacturer, displayName, manCache);

        var archName = !string.IsNullOrEmpty(cpu.Architecture)
            ? cpu.Architecture
            : "Unknown";
        var archId = $"{manufacturer}.cpu.{NormalizeId(archName)}";

        EnsureCpuArchitecture(conn, archId, manufacturer, archName, archCache);

        var familyName = InferFamilyName(cpu);
        var familyId = $"{archId}.{NormalizeId(familyName)}";
        EnsureCpuFamily(conn, familyId, archId, familyName, familyCache);

        var cpuId = $"{manufacturer}.cpu.{NormalizeId(cpu.Slug ?? cpu.Name ?? "unknown")}";

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO CpuModels
                    (Id, FamilyId, Name, Cores, Threads, BaseClock, BoostClock, Tdp)
                VALUES ($id, $fid, $name, $cores, $threads, $base, $boost, $tdp)
                """;
            cmd.Parameters.AddWithValue("$id", cpuId);
            cmd.Parameters.AddWithValue("$fid", familyId);
            cmd.Parameters.AddWithValue("$name", cpu.Name ?? cpu.Slug ?? "Unknown");
            cmd.Parameters.AddWithValue("$cores", (object?)(cpu.Cores > 0 ? cpu.Cores : null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$threads", (object?)(cpu.Threads > 0 ? cpu.Threads : null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$base", (object?)cpu.BaseClockGhz ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$boost", (object?)cpu.BoostClockGhz ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$tdp", (object?)cpu.TdpW ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        using (var acmd = conn.CreateCommand())
        {
            acmd.CommandText = "INSERT OR IGNORE INTO HardwareAliases (TargetType, TargetId, Alias, MatchMode) VALUES ($tt, $tid, $a, 'Substring')";
            acmd.Parameters.AddWithValue("$tt", "CpuModel");
            acmd.Parameters.AddWithValue("$tid", cpuId);
            acmd.Parameters.AddWithValue("$a", cpu.Name ?? cpu.Slug ?? "");
            acmd.ExecuteNonQuery();
        }
    }

    private static void EnsureManufacturer(SqliteConnection conn, string id, string displayName,
        ConcurrentDictionary<string, string> cache)
    {
        if (cache.TryGetValue(id, out _)) return;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO Manufacturers (Id, DisplayName, ShortName) VALUES ($id, $dn, $sn)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$dn", displayName);
        cmd.Parameters.AddWithValue("$sn", id);
        cmd.ExecuteNonQuery();
        cache.TryAdd(id, id);
    }

    private static void EnsureCpuArchitecture(SqliteConnection conn, string archId, string manufacturer,
        string archName, ConcurrentDictionary<string, string> cache)
    {
        if (cache.TryGetValue(archId, out _)) return;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO CpuArchitectures
                (Id, ManufacturerId, Name, DisplayName, Segment)
            VALUES ($id, $mid, $name, $disp, 'Desktop')
            """;
        cmd.Parameters.AddWithValue("$id", archId);
        cmd.Parameters.AddWithValue("$mid", manufacturer);
        cmd.Parameters.AddWithValue("$name", archName);
        cmd.Parameters.AddWithValue("$disp", archName);
        cmd.ExecuteNonQuery();
        cache.TryAdd(archId, archId);
    }

    private static void EnsureCpuFamily(SqliteConnection conn, string familyId, string archId,
        string familyName, ConcurrentDictionary<string, string> cache)
    {
        if (cache.TryGetValue(familyId, out _)) return;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO CpuFamilies (Id, ArchitectureId, Name, DisplayName) VALUES ($id, $aid, $name, $disp)";
        cmd.Parameters.AddWithValue("$id", familyId);
        cmd.Parameters.AddWithValue("$aid", archId);
        cmd.Parameters.AddWithValue("$name", familyName);
        cmd.Parameters.AddWithValue("$disp", familyName);
        cmd.ExecuteNonQuery();
        cache.TryAdd(familyId, familyId);
    }

    private static string InferFamilyName(TechApiCpuResponse cpu)
    {
        var name = cpu.Name ?? cpu.Slug ?? "";
        if (name.Contains("Ryzen 9", StringComparison.OrdinalIgnoreCase)) return "Ryzen 9";
        if (name.Contains("Ryzen 7", StringComparison.OrdinalIgnoreCase)) return "Ryzen 7";
        if (name.Contains("Ryzen 5", StringComparison.OrdinalIgnoreCase)) return "Ryzen 5";
        if (name.Contains("Ryzen 3", StringComparison.OrdinalIgnoreCase)) return "Ryzen 3";
        if (name.Contains("Threadripper", StringComparison.OrdinalIgnoreCase)) return "Threadripper";
        if (name.Contains("EPYC", StringComparison.OrdinalIgnoreCase)) return "EPYC";
        if (name.Contains("Core Ultra 9", StringComparison.OrdinalIgnoreCase)) return "Core Ultra 9";
        if (name.Contains("Core Ultra 7", StringComparison.OrdinalIgnoreCase)) return "Core Ultra 7";
        if (name.Contains("Core Ultra 5", StringComparison.OrdinalIgnoreCase)) return "Core Ultra 5";
        if (name.Contains("Core i9", StringComparison.OrdinalIgnoreCase)) return "Core i9";
        if (name.Contains("Core i7", StringComparison.OrdinalIgnoreCase)) return "Core i7";
        if (name.Contains("Core i5", StringComparison.OrdinalIgnoreCase)) return "Core i5";
        if (name.Contains("Core i3", StringComparison.OrdinalIgnoreCase)) return "Core i3";
        if (name.Contains("Xeon", StringComparison.OrdinalIgnoreCase)) return "Xeon";
        if (name.Contains("Pentium", StringComparison.OrdinalIgnoreCase)) return "Pentium";
        if (name.Contains("Celeron", StringComparison.OrdinalIgnoreCase)) return "Celeron";
        if (name.Contains("Atom", StringComparison.OrdinalIgnoreCase)) return "Atom";
        if (name.Contains("Athlon", StringComparison.OrdinalIgnoreCase)) return "Athlon";
        if (name.Contains("Opteron", StringComparison.OrdinalIgnoreCase)) return "Opteron";
        if (name.Contains("FX", StringComparison.OrdinalIgnoreCase)) return "FX";
        if (name.Contains("A-Series", StringComparison.OrdinalIgnoreCase)) return "A-Series";
        return cpu.Segment ?? "Desktop";
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
}

internal sealed class CpuListPage
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("previous")]
    public string? Previous { get; set; }

    [JsonPropertyName("results")]
    public List<CpuListEntry>? Results { get; set; }
}

internal sealed class CpuListEntry
{
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class TechApiManufacturerRef
{
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class TechApiCpuResponse
{
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("manufacturer")]
    public TechApiManufacturerRef? Manufacturer { get; set; }

    [JsonPropertyName("segment")]
    public string? Segment { get; set; }

    [JsonPropertyName("architecture")]
    public string? Architecture { get; set; }

    [JsonPropertyName("socket")]
    public string? Socket { get; set; }

    [JsonPropertyName("cores")]
    public int Cores { get; set; }

    [JsonPropertyName("threads")]
    public int Threads { get; set; }

    [JsonPropertyName("base_clock_ghz")]
    public double? BaseClockGhz { get; set; }

    [JsonPropertyName("boost_clock_ghz")]
    public double? BoostClockGhz { get; set; }

    [JsonPropertyName("l3_cache_mb")]
    public double? L3CacheMb { get; set; }

    [JsonPropertyName("tdp_w")]
    public int? TdpW { get; set; }

    [JsonPropertyName("max_tdp_w")]
    public int? MaxTdpW { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }
}
