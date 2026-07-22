using EME.Diagnostics.Core.Models;

namespace EME.Diagnostics.Core.Services;

public interface ICpuStressEngine
{
    bool IsRunning { get; }
    event EventHandler<CpuStressMetrics>? MetricsUpdated;
    Task RunAsync(CpuStressOptions options, CancellationToken cancellationToken = default);
}
