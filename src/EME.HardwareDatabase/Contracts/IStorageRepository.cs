using EME.HardwareDatabase.Detection;
using EME.HardwareDatabase.Models;

namespace EME.HardwareDatabase.Contracts;

public interface IStorageRepository
{
    Task<StorageDevice?> FindAsync(StorageDetectionIdentity identity, CancellationToken ct = default);
}
