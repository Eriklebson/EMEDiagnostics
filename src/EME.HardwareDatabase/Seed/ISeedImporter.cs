namespace EME.HardwareDatabase.Seed;

public interface ISeedImporter
{
    string SourceName { get; }
    Task<SeedResult> ImportAsync(CancellationToken ct = default);
}

public sealed record SeedResult(string SourceName, int RecordsImported, int Errors, IReadOnlyList<string>? ErrorMessages = null);
