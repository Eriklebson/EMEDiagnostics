using EME.Diagnostics.Core.Models;

namespace EME.Diagnostics.Core.Services;

public interface IReportService
{
    Task<string> ExportPdfAsync(long reportId, string destinationPath, CancellationToken ct = default);
    Task ExportAllPdfAsync(string destinationPath, CancellationToken ct = default);
}
