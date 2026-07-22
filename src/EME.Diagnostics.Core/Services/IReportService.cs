namespace EME.Diagnostics.Core.Services;

public interface IReportService
{
    Task<string> ExportPdfAsync(string destinationPath, CancellationToken cancellationToken = default);
}
