using EME.Diagnostics.Core.Models;

namespace EME.Diagnostics.Core.Services;

public interface IMemoryStressEngine
{
    bool IsRunning { get; }
    event EventHandler<MemoryStressMetrics>? MetricsUpdated;
    Task RunAsync(MemoryStressOptions options, CancellationToken cancellationToken = default);
}
