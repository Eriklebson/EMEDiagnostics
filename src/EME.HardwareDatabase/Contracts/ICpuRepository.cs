using EME.HardwareDatabase.Detection;
using EME.HardwareDatabase.Models;

namespace EME.HardwareDatabase.Contracts;

public interface ICpuRepository
{
    Task<CpuArchitecture?> FindArchitectureAsync(CpuDetectionIdentity identity, CancellationToken ct = default);
    Task<CpuFamily?> FindFamilyAsync(CpuDetectionIdentity identity, CancellationToken ct = default);
    Task<CpuModel?> FindModelAsync(CpuDetectionIdentity identity, CancellationToken ct = default);
    Task<List<CpuSensorMapping>> GetSensorMappingsAsync(string? modelId, string? architectureId, CancellationToken ct = default);
}
