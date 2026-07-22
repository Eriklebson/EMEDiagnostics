using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;

namespace EME.Diagnostics.Services;

public sealed class UnavailableGpuStressEngine : IGpuStressEngine
{
    public string BackendName => "Não instalado";
    public bool IsAvailable => false;
    public event EventHandler<GpuStressMetrics>? MetricsUpdated { add { } remove { } }
    public Task InitializeAsync(nint renderTargetHandle, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StartAsync(GpuStressOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException("O motor de stress da GPU ainda não foi implementado.");
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
