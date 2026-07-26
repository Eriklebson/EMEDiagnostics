using System.Text.Json;
using EME.HardwareDatabase.Contracts;
using EME.HardwareDatabase.Detection;
using EME.HardwareDatabase.Services;
using LibreHardwareMonitor.Hardware;

namespace EME.Diagnostics.Hardware;

public sealed class CpuSensorMappingResolver
{
    private Dictionary<string, ArchitectureEntry> _architectures = new();
    private Dictionary<string, CpuSensorSet> _vendorDefaults = new();
    private string? _matchedArchitecture;
    private string? _detectedVendor;
    private bool _loaded;
    private ICpuRepository? _dbRepo;

    public void Load()
    {
        if (_loaded) return;
        try
        {
            var factory = new DatabaseConnectionFactory();
            if (File.Exists(EME.HardwareDatabase.Shared.Constants.DatabasePath))
            {
                _dbRepo = new CpuRepository(factory);
            }
        }
        catch { }

        try
        {
            var paths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "config", "cpu-sensors-mapping.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "config", "cpu-sensors-mapping.json"),
                @"config\cpu-sensors-mapping.json"
            };
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var doc = JsonSerializer.Deserialize<CpuMappingRoot>(json, options);
                    if (doc?.Architectures != null)
                    {
                        _architectures = doc.Architectures;
                        _vendorDefaults = doc.VendorDefaults ?? new();
                        _loaded = true;
                        return;
                    }
                }
            }
        }
        catch { }
    }

    public void DetectCpu(string cpuName)
    {
        if (_detectedVendor != null) return;

        if (_dbRepo != null)
        {
            var identity = new CpuDetectionIdentity { Name = cpuName };
            var resolved = Task.Run(async () => await new HardwareProfileResolver(
                _dbRepo, new MotherboardRepository(new DatabaseConnectionFactory())).ResolveCpuAsync(identity)).GetAwaiter().GetResult();
            if (resolved.MatchLevel == MatchLevel.Exact || resolved.MatchLevel == MatchLevel.Family)
            {
                _matchedArchitecture = resolved.ProfileId;
                _detectedVendor = resolved.ProfileId?.StartsWith("amd") == true ? "AMD"
                    : resolved.ProfileId?.StartsWith("intel") == true ? "Intel"
                    : resolved.ProfileId?.StartsWith("nvidia") == true ? "NVIDIA" : null;
                return;
            }
        }

        foreach (var (archName, entry) in _architectures)
        {
            if (entry.Match == null) continue;
            foreach (var pattern in entry.Match)
            {
                if (cpuName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    _matchedArchitecture = archName;
                    _detectedVendor = entry.Vendor;
                    return;
                }
            }
        }
        if (cpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            cpuName.Contains("Ryzen", StringComparison.OrdinalIgnoreCase))
            _detectedVendor = "AMD";
        else if (cpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                 cpuName.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                 cpuName.Contains("Xeon", StringComparison.OrdinalIgnoreCase))
            _detectedVendor = "Intel";
    }

    public double? FindSensor(IHardware hardware, SensorType type, string preferred, string[]? fallbacks)
    {
        var sensors = hardware.Sensors
            .Where(x => x.SensorType == type && x.Value.HasValue)
            .ToArray();
        if (!string.IsNullOrEmpty(preferred))
        {
            var match = sensors.FirstOrDefault(x =>
                x.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(preferred, StringComparison.OrdinalIgnoreCase));
            if (match?.Value is float v) return v;
        }
        if (fallbacks != null)
        {
            foreach (var fb in fallbacks)
            {
                if (string.IsNullOrEmpty(fb)) continue;
                var match = sensors.FirstOrDefault(x =>
                    x.Name.Equals(fb, StringComparison.OrdinalIgnoreCase) ||
                    x.Name.Contains(fb, StringComparison.OrdinalIgnoreCase));
                if (match?.Value is float v) return v;
            }
        }
        return sensors.FirstOrDefault()?.Value;
    }

    public string? GetTempSensorName()
    {
        return GetSensorValue("TempSensor") ??
               GetVendorDefault("TempSensor") ??
               "Core (Tctl/Tdie)";
    }

    public string? GetPowerSensorName()
    {
        return GetSensorValue("PowerSensor") ??
               GetVendorDefault("PowerSensor") ??
               "CPU Package";
    }

    public string[] GetTempFallbacks()
    {
        var archFallback = GetSensorList("TempFallback");
        if (archFallback.Length > 0) return archFallback;
        var vendorFallback = GetVendorDefaultList("TempFallback");
        if (vendorFallback.Length > 0) return vendorFallback;
        return [];
    }

    private string? GetSensorValue(string key)
    {
        if (_matchedArchitecture == null || !_architectures.TryGetValue(_matchedArchitecture, out var entry))
            return null;
        return entry.Sensors?.GetValue(key);
    }

    private string[] GetSensorList(string key)
    {
        if (_matchedArchitecture == null || !_architectures.TryGetValue(_matchedArchitecture, out var entry))
            return [];
        return entry.Sensors?.GetList(key) ?? [];
    }

    private string? GetVendorDefault(string key)
    {
        if (_detectedVendor == null || !_vendorDefaults.TryGetValue(_detectedVendor, out var defaults))
            return null;
        return defaults.GetValue(key);
    }

    private string[] GetVendorDefaultList(string key)
    {
        if (_detectedVendor == null || !_vendorDefaults.TryGetValue(_detectedVendor, out var defaults))
            return [];
        return defaults.GetList(key) ?? [];
    }

    public string? DetectedArchitecture => _matchedArchitecture;
    public string? DetectedVendor => _detectedVendor;
}

internal sealed class CpuMappingRoot
{
    public Dictionary<string, ArchitectureEntry>? Architectures { get; set; }
    public Dictionary<string, CpuSensorSet>? VendorDefaults { get; set; }
}

internal sealed class ArchitectureEntry
{
    public List<string>? Match { get; set; }
    public string? Vendor { get; set; }
    public CpuSensorSet? Sensors { get; set; }
}

internal sealed class CpuSensorSet
{
    public string? TempSensor { get; set; }
    public string? PowerSensor { get; set; }
    public List<string>? TempFallback { get; set; }

    public string? GetValue(string key) => key switch
    {
        "TempSensor" => TempSensor,
        "PowerSensor" => PowerSensor,
        _ => null
    };

    public string[] GetList(string key) => key switch
    {
        "TempFallback" => TempFallback?.ToArray() ?? [],
        _ => []
    };
}
