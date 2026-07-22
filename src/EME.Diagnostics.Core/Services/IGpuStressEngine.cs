using EME.Diagnostics.Core.Models;

namespace EME.Diagnostics.Core.Services;

public interface IGpuStressEngine : IAsyncDisposable
{
    string BackendName { get; }
    bool IsAvailable { get; }
    event EventHandler<GpuStressMetrics>? MetricsUpdated;
    Task InitializeAsync(nint renderTargetHandle, CancellationToken cancellationToken = default);
    Task StartAsync(GpuStressOptions options, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
