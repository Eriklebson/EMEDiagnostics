using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Services;

namespace EME.HardwareDatabase.Seed;

public sealed class RightNowGpuImporter : ISeedImporter
{
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly HttpClient _http;

    private static readonly string[] SourceUrls =
    [
        "https://raw.githubusercontent.com/RightNow-AI/RightNow-GPU-Database/main/data/nvidia/all.json",
        "https://raw.githubusercontent.com/RightNow-AI/RightNow-GPU-Database/main/data/amd/all.json",
        "https://raw.githubusercontent.com/RightNow-AI/RightNow-GPU-Database/main/data/intel/all.json"
    ];

    public string SourceName => "RightNow GPU Database";

    public RightNowGpuImporter(DatabaseConnectionFactory connectionFactory, HttpClient? http = null)
    {
        _connectionFactory = connectionFactory;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<SeedResult> ImportAsync(CancellationToken ct = default)
    {
        var totalImported = 0;
        var totalErrors = 0;
        var errors = new ConcurrentBag<string>();

        var architectureCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var manufacturerCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var url in SourceUrls)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                Debug.WriteLine($"Baixando GPUs de: {url}");
                var response = await _http.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(ct);
                var gpus = JsonSerializer.Deserialize<List<RightNowGpuEntry>>(content);
                if (gpus == null || gpus.Count == 0) continue;

                var batch = gpus.Where(g => g.Vendor != null && g.Name != null).ToList();
                Debug.WriteLine($"  Processando {batch.Count} GPUs de {url}");

                using var conn = _connectionFactory.CreateConnection();
                using var tx = conn.BeginTransaction();

                foreach (var gpu in batch)
                {
                    try
                    {
                        ImportSingleGpu(conn, gpu, architectureCache, manufacturerCache);
                        totalImported++;
                    }
                    catch (Exception ex)
                    {
                        totalErrors++;
                        errors.Add($"Erro ao importar GPU '{gpu.Name}': {ex.Message}");
                    }
                }

                tx.Commit();
                Debug.WriteLine($"  Commit {batch.Count} GPUs concluído.");
            }
            catch (Exception ex)
            {
                totalErrors++;
                errors.Add($"Falha ao baixar/processar {url}: {ex.Message}");
            }
        }

        return new SeedResult(SourceName, totalImported, totalErrors,
            errors.IsEmpty ? null : errors.ToList().AsReadOnly());
    }

    private void ImportSingleGpu(SqliteConnection conn, RightNowGpuEntry gpu,
        ConcurrentDictionary<string, string> archCache,
        ConcurrentDictionary<string, string> manCache)
    {
        var vendor = gpu.Vendor!.ToLowerInvariant();
        var manufacturer = gpu.Manufacturer ?? char.ToUpperInvariant(vendor[0]) + vendor[1..];

        EnsureManufacturer(conn, vendor, manufacturer, manCache);

        var archId = EnsureGpuArchitecture(conn, gpu, vendor, archCache);

        var gpuId = $"{vendor}.gpu.{NormalizeId(gpu.Id ?? gpu.Name!)}";
        var vramMb = gpu.MemorySize.HasValue
            ? (int?)(gpu.MemorySize.Value * 1024)
            : null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO GpuModels
                    (Id, ArchitectureId, Name, VramMb, VramType)
                VALUES ($id, $aid, $name, $vram, $vtype)
                """;
            cmd.Parameters.AddWithValue("$id", gpuId);
            cmd.Parameters.AddWithValue("$aid", archId);
            cmd.Parameters.AddWithValue("$name", gpu.Name!);
            cmd.Parameters.AddWithValue("$vram", (object?)vramMb ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$vtype", (object?)gpu.MemoryType ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        using (var acmd = conn.CreateCommand())
        {
            acmd.CommandText = "INSERT OR IGNORE INTO HardwareAliases (TargetType, TargetId, Alias, MatchMode) VALUES ($tt, $tid, $a, 'Substring')";
            acmd.Parameters.AddWithValue("$tt", "GpuModel");
            acmd.Parameters.AddWithValue("$tid", gpuId);
            acmd.Parameters.AddWithValue("$a", gpu.Name!);
            acmd.ExecuteNonQuery();
        }
    }

    private string EnsureGpuArchitecture(SqliteConnection conn, RightNowGpuEntry gpu,
        string vendor, ConcurrentDictionary<string, string> cache)
    {
        var archName = !string.IsNullOrEmpty(gpu.Architecture)
            ? gpu.Architecture
            : !string.IsNullOrEmpty(gpu.Generation)
                ? gpu.Generation
                : "Unknown";

        var archId = $"{vendor}.gpu.{NormalizeId(archName)}";

        if (cache.TryGetValue(archId, out var existing))
            return existing;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO GpuArchitectures
                    (Id, ManufacturerId, Name, DisplayName, Segment)
                VALUES ($id, $mid, $name, $disp, $seg)
                """;
            cmd.Parameters.AddWithValue("$id", archId);
            cmd.Parameters.AddWithValue("$mid", vendor);
            cmd.Parameters.AddWithValue("$name", archName);
            cmd.Parameters.AddWithValue("$disp", $"{gpu.Manufacturer ?? vendor} {archName}");
            cmd.Parameters.AddWithValue("$seg", InferGpuSegment(gpu));
            cmd.ExecuteNonQuery();
        }

        cache.TryAdd(archId, archId);
        return archId;
    }

    private static string InferGpuSegment(RightNowGpuEntry gpu)
    {
        var gen = gpu.Generation ?? "";
        if (gen.Contains("Quadro", StringComparison.OrdinalIgnoreCase) ||
            gen.Contains("RTX", StringComparison.OrdinalIgnoreCase) ||
            gen.Contains("Radeon Pro", StringComparison.OrdinalIgnoreCase) ||
            gen.Contains("FirePro", StringComparison.OrdinalIgnoreCase) ||
            gen.Contains("Instinct", StringComparison.OrdinalIgnoreCase) ||
            gen.Contains("Tesla", StringComparison.OrdinalIgnoreCase) ||
            gen.Contains("A-Series", StringComparison.OrdinalIgnoreCase) ||
            (gpu.Name?.Contains("RTX", StringComparison.OrdinalIgnoreCase) == true &&
             gpu.Name?.Contains("A", StringComparison.OrdinalIgnoreCase) != true &&
             gpu.Name?.Contains("RTX A", StringComparison.OrdinalIgnoreCase) != true))
            return gpu.Name?.Contains("RTX A", StringComparison.OrdinalIgnoreCase) == true
                ? "Workstation"
                : "Consumer";
        if (gen.Contains("Console", StringComparison.OrdinalIgnoreCase))
            return "Console";
        if (gen.Contains("Go", StringComparison.OrdinalIgnoreCase) ||
            gen.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
            gen.Contains("Mobility", StringComparison.OrdinalIgnoreCase) ||
            gen.Contains("IGP", StringComparison.OrdinalIgnoreCase) ||
            gen.Contains("MX", StringComparison.OrdinalIgnoreCase) ||
            (gpu.Tdp.HasValue && gpu.Tdp < 30))
            return "Mobile";
        if ((gpu.Name?.Contains("A", StringComparison.OrdinalIgnoreCase) == true &&
             gpu.Vendor == "nvidia") ||
            gen.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) ||
            gen.Contains("Server", StringComparison.OrdinalIgnoreCase))
            return "Workstation";
        return "Consumer";
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

internal sealed class RightNowGpuEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("vendor")]
    public string? Vendor { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("gpuName")]
    public string? GpuName { get; set; }

    [JsonPropertyName("architecture")]
    public string? Architecture { get; set; }

    [JsonPropertyName("generation")]
    public string? Generation { get; set; }

    [JsonPropertyName("memorySize")]
    public double? MemorySize { get; set; }

    [JsonPropertyName("memoryType")]
    public string? MemoryType { get; set; }

    [JsonPropertyName("tdp")]
    public int? Tdp { get; set; }

    [JsonPropertyName("baseClock")]
    public double? BaseClock { get; set; }

    [JsonPropertyName("boostClock")]
    public double? BoostClock { get; set; }
}
