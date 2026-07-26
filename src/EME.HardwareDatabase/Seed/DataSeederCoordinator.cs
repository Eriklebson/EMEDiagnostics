using System.Diagnostics;
using EME.HardwareDatabase.Services;
using EME.HardwareDatabase.Shared;

namespace EME.HardwareDatabase.Seed;

public sealed class DataSeederCoordinator
{
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly DatabaseVersionService _versionService;
    private readonly HttpClient _http;
    private readonly List<ISeedImporter> _networkImporters;
    private readonly List<ISeedImporter> _localSeeders;

    public DataSeederCoordinator(DatabaseConnectionFactory connectionFactory,
        DatabaseVersionService? versionService = null,
        HttpClient? http = null)
    {
        _connectionFactory = connectionFactory;
        _versionService = versionService ?? new DatabaseVersionService(connectionFactory);
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "EME.HardwareDatabase/1.0");

        _networkImporters =
        [
            new RightNowGpuImporter(_connectionFactory, _http),
            new TechApiCpuImporter(_connectionFactory, _http),
        ];

        _localSeeders =
        [
            new GpuSensorMappingSeeder(_connectionFactory),
            new CpuSensorMappingSeeder(_connectionFactory),
            new MotherboardMappingSeeder(_connectionFactory),
            new ReferenceDataSeeder(_connectionFactory),
        ];
    }

    public async Task<DataSeedSummary> SeedAllAsync(CancellationToken ct = default)
    {
        var results = new List<SeedResult>();
        var stopwatch = Stopwatch.StartNew();

        results.AddRange(await RunImporters(_networkImporters, ct));
        results.AddRange(await RunImporters(_localSeeders, ct));

        stopwatch.Stop();
        var summary = new DataSeedSummary(results, stopwatch.Elapsed);

        if (summary.TotalImported > 0)
        {
            try
            {
                _versionService.UpdateMetadata(Constants.SchemaVersion, Constants.DataVersion);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Aviso: não foi possível atualizar DataVersion: {ex.Message}");
            }
        }

        return summary;
    }

    public async Task<DataSeedSummary> SeedIfEmptyAsync(CancellationToken ct = default)
    {
        var results = new List<SeedResult>();
        var stopwatch = Stopwatch.StartNew();

        using (var conn = _connectionFactory.CreateReadOnlyConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM GpuModels";
            var gpuCount = Convert.ToInt32(cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM CpuModels";
            var cpuCount = Convert.ToInt32(cmd.ExecuteScalar());

            if (gpuCount == 0 || cpuCount == 0)
            {
                Debug.WriteLine("Banco vazio ou incompleto. Iniciando importação de rede...");
                results.AddRange(await RunImporters(_networkImporters, ct));
            }
            else
            {
                Debug.WriteLine($"Banco já populado: {gpuCount} GPUs, {cpuCount} CPUs.");
            }
        }

        // Local seeders sempre rodam (rápidos, idempotentes, sem rede)
        results.AddRange(await RunImporters(_localSeeders, ct));

        stopwatch.Stop();
        var summary = new DataSeedSummary(results, stopwatch.Elapsed);

        if (summary.TotalImported > 0)
        {
            try
            {
                _versionService.UpdateMetadata(Constants.SchemaVersion, Constants.DataVersion);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Aviso: não foi possível atualizar DataVersion: {ex.Message}");
            }
        }

        return summary;
    }

    private async Task<List<SeedResult>> RunImporters(IEnumerable<ISeedImporter> importers, CancellationToken ct)
    {
        var results = new List<SeedResult>();
        foreach (var importer in importers)
        {
            if (ct.IsCancellationRequested) break;

            Debug.WriteLine($"Iniciando importação: {importer.SourceName}");
            try
            {
                var result = await importer.ImportAsync(ct);
                results.Add(result);
                Debug.WriteLine($"  {importer.SourceName}: {result.RecordsImported} registros, {result.Errors} erros");
            }
            catch (Exception ex)
            {
                results.Add(new SeedResult(importer.SourceName, 0, 1, [ex.Message]));
                Debug.WriteLine($"  {importer.SourceName}: FALHA - {ex.Message}");
            }
        }
        return results;
    }
}

public sealed record DataSeedSummary(IReadOnlyList<SeedResult> Results, TimeSpan Elapsed)
{
    public int TotalImported => Results.Sum(r => r.RecordsImported);
    public int TotalErrors => Results.Sum(r => r.Errors);
    public bool HasErrors => Results.Any(r => r.Errors > 0);

    public string GetReport()
    {
        var lines = new List<string>
        {
            $"Seed concluído em {Elapsed.TotalSeconds:F1}s",
            $"Total: {TotalImported} registros importados, {TotalErrors} erros"
        };
        foreach (var r in Results)
        {
            lines.Add($"  {r.SourceName}: {r.RecordsImported} importados, {r.Errors} erros");
            if (r.ErrorMessages?.Count > 0)
            {
                foreach (var err in r.ErrorMessages.Take(5))
                    lines.Add($"    - {err}");
                if (r.ErrorMessages.Count > 5)
                    lines.Add($"    ... e mais {r.ErrorMessages.Count - 5} erros");
            }
        }
        return string.Join(Environment.NewLine, lines);
    }
}
