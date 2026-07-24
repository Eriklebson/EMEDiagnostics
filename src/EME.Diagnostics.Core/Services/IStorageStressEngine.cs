using EME.Diagnostics.Core.Models;

namespace EME.Diagnostics.Core.Services;

public interface IStorageStressEngine
{
    bool IsRunning { get; }
    event EventHandler<StorageStressMetrics>? MetricsUpdated;
    Task RunAsync(StorageStressOptions options, CancellationToken cancellationToken = default);
}
