using EME.HardwareDatabase.Detection;
using EME.HardwareDatabase.Models;

namespace EME.HardwareDatabase.Contracts;

public interface IGpuRepository
{
    Task<GpuModel?> FindAsync(GpuDetectionIdentity identity, CancellationToken ct = default);
    Task<List<GpuSensorMapping>> GetSensorMappingsAsync(string gpuModelId, CancellationToken ct = default);
}
