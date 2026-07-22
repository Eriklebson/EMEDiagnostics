using System.Text.Json;

namespace EME.Diagnostics.Hardware;

internal sealed class HardwareMappingResolver
{
    private readonly HardwareMappingConfig _config;
    private MotherboardMapping? _motherboard;

    public HardwareMappingResolver()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config", "hardware-mapping.json");
        if (!File.Exists(path))
        {
            _config = new HardwareMappingConfig();
            return;
        }

        try
        {
            _config = JsonSerializer.Deserialize<HardwareMappingConfig>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new HardwareMappingConfig();
        }
        catch
        {
            _config = new HardwareMappingConfig();
        }
    }

    public void DetectMotherboard(string? motherboardName)
    {
        if (string.IsNullOrWhiteSpace(motherboardName)) return;
        _motherboard = _config.Motherboards.FirstOrDefault(mapping =>
            motherboardName.Contains(mapping.Name, StringComparison.OrdinalIgnoreCase) ||
            mapping.Name.Contains(motherboardName, StringComparison.OrdinalIgnoreCase));
    }

    public (string Name, string Category) ResolveFan(string rawName)
    {
        var mappedName = _motherboard?.FanMapping.TryGetValue(rawName, out var mapped) == true ? mapped : rawName;
        var category = IsCpuCoolingName(mappedName) ? "CPU" : "Motherboard";
        return (mappedName, category);
    }

    private static bool IsCpuCoolingName(string name) =>
        name.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("PUMP", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("AIO", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("WATER", StringComparison.OrdinalIgnoreCase);

    private sealed class HardwareMappingConfig
    {
        public List<MotherboardMapping> Motherboards { get; init; } = new();
    }

    private sealed class MotherboardMapping
    {
        public string Name { get; init; } = string.Empty;
        public Dictionary<string, string> FanMapping { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
