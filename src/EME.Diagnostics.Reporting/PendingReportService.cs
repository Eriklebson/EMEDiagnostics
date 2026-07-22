using EME.Diagnostics.Core.Services;

namespace EME.Diagnostics.Reporting;

public sealed class PendingReportService : IReportService
{
    public Task<string> ExportPdfAsync(string destinationPath, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A exportação em PDF será implementada em uma fase futura.");
}
