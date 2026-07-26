using EME.HardwareDatabase.Detection;
using EME.HardwareDatabase.Models;

namespace EME.HardwareDatabase.Contracts;

public interface IMemoryRepository
{
    Task<MemoryModel?> FindAsync(MemoryDetectionIdentity identity, CancellationToken ct = default);
}
