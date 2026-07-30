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
        DiagnosticLogger.Log("Load() iniciado");
        if (_loaded)
        {
            DiagnosticLogger.Log("Load() ignorado: ja carregado");
            return;
        }

        try
        {
            var factory = new DatabaseConnectionFactory();
            var dbPath = EME.HardwareDatabase.Shared.Constants.DatabasePath;
            if (File.Exists(dbPath))
            {
                var fi = new System.IO.FileInfo(dbPath);
                DiagnosticLogger.Log($"DB encontrado: {fi.Length} bytes, criado em {fi.CreationTime}");
                _dbRepo = new CpuRepository(factory);
                DiagnosticLogger.Log("CpuRepository criado com sucesso");
            }
            else
            {
                DiagnosticLogger.Log($"DB nao encontrado em: {dbPath}");
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Log($"Falha ao criar CpuRepository: {ex.GetType().Name}: {ex.Message}");
        }

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
                DiagnosticLogger.Log($"Procurando config JSON em: {path} (existe: {File.Exists(path)})");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var doc = JsonSerializer.Deserialize<CpuMappingRoot>(json, options);
                    if (doc?.Architectures != null)
                    {
                        _architectures = doc.Architectures;
                        _vendorDefaults = doc.VendorDefaults ?? new();
                        _loaded = true;
                        DiagnosticLogger.Log($"JSON config carregado: {_architectures.Count} architectures, {_vendorDefaults.Count} vendor defaults");
                        return;
                    }
                    else
                    {
                        DiagnosticLogger.Log("JSON config encontrado mas Architectures e null");
                    }
                }
            }
            DiagnosticLogger.Log("Nenhum JSON config encontrado ou valido");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Log($"Falha ao carregar JSON config: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void DetectCpu(string cpuName)
    {
        DiagnosticLogger.Log($"DetectCpu('{cpuName}') iniciado. _detectedVendor atual: {_detectedVendor ?? "(null)"}");
        if (_detectedVendor != null)
        {
            DiagnosticLogger.Log("DetectCpu ignorado: vendor ja detectado");
            return;
        }

        if (_dbRepo != null)
        {
            try
            {
                var identity = new CpuDetectionIdentity { Name = cpuName };
                DiagnosticLogger.Log("Consultando HardwareProfileResolver...");
                var resolved = Task.Run(async () => await new HardwareProfileResolver(
                    _dbRepo, new MotherboardRepository(new DatabaseConnectionFactory())).ResolveCpuAsync(identity)).GetAwaiter().GetResult();
                DiagnosticLogger.Log($"DB resolveu: MatchLevel={resolved.MatchLevel}, ProfileId={resolved.ProfileId ?? "(null)"}, DisplayName={resolved.DisplayName ?? "(null)"}");
                if (resolved.MatchLevel == MatchLevel.Exact || resolved.MatchLevel == MatchLevel.Family)
                {
                    _matchedArchitecture = resolved.ProfileId;
                    _detectedVendor = resolved.ProfileId?.StartsWith("amd") == true ? "AMD"
                        : resolved.ProfileId?.StartsWith("intel") == true ? "Intel"
                        : resolved.ProfileId?.StartsWith("nvidia") == true ? "NVIDIA" : null;
                    DiagnosticLogger.Log($"DB match! Arch='{_matchedArchitecture}', Vendor='{_detectedVendor}'");
                    return;
                }
                DiagnosticLogger.Log("DB nao resolveu (MatchLevel nao e Exact/Family), tentando JSON config");
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Log($"Falha no HardwareProfileResolver: {ex.GetType().Name}: {ex.Message}");
            }
        }
        else
        {
            DiagnosticLogger.Log("DB nao disponivel, tentando JSON config");
        }

        DiagnosticLogger.Log($"Procurando match em JSON config ({_architectures.Count} architectures)...");
        foreach (var (archName, entry) in _architectures)
        {
            if (entry.Match == null) continue;
            foreach (var pattern in entry.Match)
            {
                if (cpuName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    _matchedArchitecture = archName;
                    _detectedVendor = entry.Vendor;
                    DiagnosticLogger.Log($"JSON config match! Pattern='{pattern}', Arch='{archName}', Vendor='{entry.Vendor}'");
                    return;
                }
            }
        }

        DiagnosticLogger.Log("JSON config nao matchou, tentando deteccao por nome do vendor");
        if (cpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            cpuName.Contains("Ryzen", StringComparison.OrdinalIgnoreCase))
        {
            _detectedVendor = "AMD";
            DiagnosticLogger.Log("Vendor detectado como AMD (por nome)");
        }
        else if (cpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                 cpuName.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                 cpuName.Contains("Xeon", StringComparison.OrdinalIgnoreCase))
        {
            _detectedVendor = "Intel";
            DiagnosticLogger.Log("Vendor detectado como Intel (por nome)");
        }
        else
        {
            DiagnosticLogger.Log($"Vendor NAO DETECTADO para CPU '{cpuName}'");
        }
    }

    public double? FindSensor(IHardware hardware, SensorType type, string preferred, string[]? fallbacks)
    {
        var sensors = hardware.Sensors
            .Where(x => x.SensorType == type && x.Value.HasValue)
            .ToArray();
        DiagnosticLogger.Log($"FindSensor({type}, preferred='{preferred}'): {hardware.Name} tem {hardware.Sensors.Length} sensors do tipo {type}, {sensors.Length} com valor");

        if (!string.IsNullOrEmpty(preferred))
        {
            var match = sensors.FirstOrDefault(x =>
                x.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(preferred, StringComparison.OrdinalIgnoreCase));
            if (match?.Value is float v)
            {
                DiagnosticLogger.Log($"FindSensor: match preferred '{preferred}' = {v}");
                return v;
            }
            DiagnosticLogger.Log($"FindSensor: preferred '{preferred}' nao encontrou match");
        }
        if (fallbacks != null)
        {
            foreach (var fb in fallbacks)
            {
                if (string.IsNullOrEmpty(fb)) continue;
                var match = sensors.FirstOrDefault(x =>
                    x.Name.Equals(fb, StringComparison.OrdinalIgnoreCase) ||
                    x.Name.Contains(fb, StringComparison.OrdinalIgnoreCase));
                if (match?.Value is float v)
                {
                    DiagnosticLogger.Log($"FindSensor: match fallback '{fb}' = {v}");
                    return v;
                }
            }
        }
        var first = sensors.FirstOrDefault()?.Value;
        DiagnosticLogger.Log($"FindSensor: nenhum match, retornando primeiro sensor = {first?.ToString() ?? "null"}");
        return first;
    }

    public string? GetTempSensorName()
    {
        var result = GetSensorValue("TempSensor") ??
                     GetVendorDefault("TempSensor") ??
                     HardcodedVendorDefault("TempSensor") ??
                     "Core (Tctl/Tdie)";
        DiagnosticLogger.Log($"GetTempSensorName = '{result}' (arch: {GetSensorValue("TempSensor") ?? "(null)"}, vendor: {GetVendorDefault("TempSensor") ?? "(null)"}, hardcoded: {HardcodedVendorDefault("TempSensor") ?? "(null)"})");
        return result;
    }

    public string? GetPowerSensorName()
    {
        var result = GetSensorValue("PowerSensor") ??
                     GetVendorDefault("PowerSensor") ??
                     HardcodedVendorDefault("PowerSensor") ??
                     "CPU Package";
        DiagnosticLogger.Log($"GetPowerSensorName = '{result}'");
        return result;
    }

    public string[] GetTempFallbacks()
    {
        var archFallback = GetSensorList("TempFallback");
        if (archFallback.Length > 0) return archFallback;
        var vendorFallback = GetVendorDefaultList("TempFallback");
        if (vendorFallback.Length > 0) return vendorFallback;
        var hardcoded = HardcodedVendorList("TempFallback");
        if (hardcoded.Length > 0) return hardcoded;
        return [];
    }

    private string? HardcodedVendorDefault(string key)
    {
        if (_detectedVendor == null) return null;
        return (_detectedVendor, key) switch
        {
            ("AMD", "TempSensor") => "Core (Tctl/Tdie)",
            ("AMD", "PowerSensor") => "CPU Package",
            ("Intel", "TempSensor") => "CPU Package",
            ("Intel", "PowerSensor") => "CPU Package",
            _ => null
        };
    }

    private string[] HardcodedVendorList(string key)
    {
        if (_detectedVendor == null) return [];
        return (_detectedVendor, key) switch
        {
            ("AMD", "TempFallback") => ["Tctl", "Tdie", "CCD", "CPU Package", "Core"],
            ("Intel", "TempFallback") => ["Core #0", "CPU Package", "Core Average", "CPU", "Core"],
            _ => []
        };
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
