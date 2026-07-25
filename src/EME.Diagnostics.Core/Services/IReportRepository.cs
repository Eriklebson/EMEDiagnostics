using EME.Diagnostics.Core.Models;

namespace EME.Diagnostics.Core.Services;

public interface IReportRepository
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<long> SaveReportAsync(StressReportDetail report, CancellationToken ct = default);
    Task<List<StressReportSummary>> GetAllReportsAsync(CancellationToken ct = default);
    Task<StressReportDetail?> GetReportAsync(long id, CancellationToken ct = default);
    Task DeleteReportAsync(long id, CancellationToken ct = default);
}
