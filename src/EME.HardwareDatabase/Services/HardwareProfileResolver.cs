using EME.HardwareDatabase.Contracts;
using EME.HardwareDatabase.Detection;
using EME.HardwareDatabase.Models;

namespace EME.HardwareDatabase.Services;

public sealed class HardwareProfileResolver
{
    private readonly ICpuRepository _cpuRepository;
    private readonly IMotherboardRepository _motherboardRepository;

    public HardwareProfileResolver(ICpuRepository cpuRepository, IMotherboardRepository motherboardRepository)
    {
        _cpuRepository = cpuRepository;
        _motherboardRepository = motherboardRepository;
    }

    public async Task<MatchResult> ResolveCpuAsync(CpuDetectionIdentity identity, CancellationToken ct = default)
    {
        var model = await _cpuRepository.FindModelAsync(identity, ct);
        if (model != null)
            return new MatchResult { ProfileId = model.Id, MatchLevel = MatchLevel.Exact, MatchConfidence = 100, DisplayName = model.Name };

        var family = await _cpuRepository.FindFamilyAsync(identity, ct);
        if (family != null)
            return new MatchResult { ProfileId = family.Id, MatchLevel = MatchLevel.Family, MatchConfidence = 70, DisplayName = family.DisplayName ?? family.Name };

        var arch = await _cpuRepository.FindArchitectureAsync(identity, ct);
        if (arch != null)
            return new MatchResult { ProfileId = arch.Id, MatchLevel = MatchLevel.Architecture, MatchConfidence = 50, DisplayName = arch.DisplayName };

        if (identity.Vendor != null)
            return new MatchResult { ProfileId = identity.Vendor, MatchLevel = MatchLevel.Generic, MatchConfidence = 20, DisplayName = identity.Vendor };

        return new MatchResult { MatchLevel = MatchLevel.Unknown, MatchConfidence = 0 };
    }

    public async Task<MatchResult> ResolveMotherboardAsync(MotherboardDetectionIdentity identity, CancellationToken ct = default)
    {
        var mobo = await _motherboardRepository.FindAsync(identity, ct);
        if (mobo != null)
            return new MatchResult { ProfileId = mobo.Id, MatchLevel = MatchLevel.Exact, MatchConfidence = 100, DisplayName = mobo.Name };

        return new MatchResult { MatchLevel = MatchLevel.Unknown, MatchConfidence = 0 };
    }
}
