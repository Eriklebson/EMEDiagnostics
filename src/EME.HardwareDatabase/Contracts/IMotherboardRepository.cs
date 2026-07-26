using EME.HardwareDatabase.Detection;
using EME.HardwareDatabase.Models;

namespace EME.HardwareDatabase.Contracts;

public interface IMotherboardRepository
{
    Task<Motherboard?> FindAsync(MotherboardDetectionIdentity identity, CancellationToken ct = default);
    Task<List<MotherboardFanMapping>> GetFanMappingsAsync(string motherboardId, CancellationToken ct = default);
    Task<List<MotherboardTemperatureMapping>> GetTemperatureMappingsAsync(string motherboardId, CancellationToken ct = default);
    Task<List<MotherboardVoltageMapping>> GetVoltageMappingsAsync(string motherboardId, CancellationToken ct = default);
}
