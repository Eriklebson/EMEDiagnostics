using EME.Diagnostics.Core.Models;

namespace EME.Diagnostics.Core.Services;

public interface IHardwareMonitor : IDisposable
{
    Task<HardwareSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}
