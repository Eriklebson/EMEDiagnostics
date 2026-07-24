using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;
using LibreHardwareMonitor.Hardware;

namespace EME.Diagnostics.Hardware;

public sealed class LibreHardwareMonitorService : IHardwareMonitor
{
    private readonly Computer _computer;
    private readonly HardwareMappingResolver _mapping = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public LibreHardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = false,
            IsControllerEnabled = true
        };
        _computer.Open();
    }

    public async Task<HardwareSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var all = Enumerate(_computer.Hardware).ToArray();
            foreach (var hardware in all) hardware.Update();

            var cpu = all.FirstOrDefault(x => x.HardwareType == HardwareType.Cpu);
            var gpu = all.FirstOrDefault(x => x.HardwareType is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel);
            var memory = all.FirstOrDefault(x => x.HardwareType == HardwareType.Memory);
            var motherboard = all.FirstOrDefault(x => x.HardwareType == HardwareType.Motherboard);
            _mapping.DetectMotherboard(motherboard?.Name);
            var fans = all.SelectMany(x => x.Sensors)
                .Where(x => x.SensorType == SensorType.Fan && x.Value is >= 0)
                .Select(x => (Sensor: x, Mapping: _mapping.ResolveFan(x.Name)))
                .Select(x => new FanMetric(x.Mapping.Name, x.Sensor.Value ?? 0, x.Mapping.Category))
                .ToArray();

            var devices = all
                .Select(hardware => new HardwareDeviceSnapshot(
                    hardware.Name,
                    hardware.HardwareType.ToString(),
                    hardware.Identifier.ToString(),
                    FindParentName(_computer.Hardware, hardware),
                    hardware.Sensors
                        .OrderBy(sensor => sensor.SensorType)
                        .ThenBy(sensor => sensor.Name)
                        .Select(sensor => new SensorMetric(
                            sensor.Name,
                            sensor.SensorType.ToString(),
                            sensor.Value,
                            sensor.Min,
                            sensor.Max,
                            GetUnit(sensor.SensorType),
                            sensor.Identifier.ToString()))
                        .ToArray()))
                .OrderBy(device => DeviceOrder(device.Type))
                .ThenBy(device => device.Name)
                .ToArray();

            return new HardwareSnapshot(
                DateTimeOffset.Now,
                ToMetric(cpu, "CPU não detectada"),
                ToMetric(gpu, "GPU não detectada"),
                ReadMemory(memory, "Memory Used"),
                ReadMemory(memory, "Memory Available") + ReadMemory(memory, "Memory Used"),
                FindMemoryTemperature(all),
                fans,
                devices);
        }
        finally { _gate.Release(); }
    }

    private static ComponentMetric ToMetric(IHardware? hardware, string fallback)
    {
        if (hardware is null) return new(fallback, null, null, null, null);
        return new(
            hardware.Name,
            Find(hardware, SensorType.Load, "Total", "Core"),
            Find(hardware, SensorType.Temperature, "Package", "Core", "Tctl", "Hot Spot"),
            Find(hardware, SensorType.Clock, "Core", "GPU"),
            Find(hardware, SensorType.Power, "Package", "GPU"));
    }

    private static double ReadMemory(IHardware? memory, string name) =>
        memory?.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Data && x.Name.Contains(name, StringComparison.OrdinalIgnoreCase))?.Value ?? 0;

    private static double? FindMemoryTemperature(IHardware[] all)
    {
        var memoryHardware = all.FirstOrDefault(x => x.HardwareType == HardwareType.Memory);
        var temp = memoryHardware?.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Temperature && x.Value.HasValue)?.Value;
        if (temp.HasValue) return temp;
        var dimm = all.SelectMany(x => x.Sensors)
            .FirstOrDefault(x => x.SensorType == SensorType.Temperature && x.Name.Contains("DIMM", StringComparison.OrdinalIgnoreCase) && x.Value.HasValue);
        return dimm?.Value ?? all.SelectMany(x => x.Sensors)
            .FirstOrDefault(x => x.SensorType == SensorType.Temperature && x.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase) && x.Value.HasValue)?.Value;
    }

    private static double? Find(IHardware hardware, SensorType type, params string[] priorities)
    {
        var sensors = hardware.Sensors.Where(x => x.SensorType == type && x.Value.HasValue).ToArray();
        foreach (var priority in priorities)
        {
            var match = sensors.FirstOrDefault(x => x.Name.Contains(priority, StringComparison.OrdinalIgnoreCase));
            if (match?.Value is float value) return value;
        }
        return sensors.FirstOrDefault()?.Value;
    }

    private static IEnumerable<IHardware> Enumerate(IEnumerable<IHardware> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in Enumerate(root.SubHardware)) yield return child;
        }
    }

    private static string? FindParentName(IEnumerable<IHardware> roots, IHardware target)
    {
        foreach (var root in roots)
        {
            if (root.SubHardware.Contains(target)) return root.Name;
            var nested = FindParentName(root.SubHardware, target);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static int DeviceOrder(string type) => type switch
    {
        nameof(HardwareType.Cpu) => 0,
        nameof(HardwareType.GpuNvidia) or nameof(HardwareType.GpuAmd) or nameof(HardwareType.GpuIntel) => 1,
        nameof(HardwareType.Memory) => 2,
        nameof(HardwareType.Motherboard) => 3,
        nameof(HardwareType.SuperIO) or nameof(HardwareType.EmbeddedController) => 4,
        nameof(HardwareType.Storage) => 5,
        nameof(HardwareType.Network) => 6,
        _ => 20
    };

    private static string GetUnit(SensorType type) => type switch
    {
        SensorType.Voltage => "V",
        SensorType.Current => "A",
        SensorType.Clock => "MHz",
        SensorType.Load => "%",
        SensorType.Temperature => "°C",
        SensorType.Fan => "RPM",
        SensorType.Flow => "L/h",
        SensorType.Control => "%",
        SensorType.Level => "%",
        SensorType.Power => "W",
        SensorType.Data => "GB",
        SensorType.SmallData => "MB",
        SensorType.Factor => "",
        SensorType.Frequency => "Hz",
        SensorType.Throughput => "B/s",
        SensorType.TimeSpan => "s",
        SensorType.Energy => "mWh",
        SensorType.Noise => "dBA",
        SensorType.Conductivity => "µS/cm",
        SensorType.Humidity => "%",
        _ => ""
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _computer.Close();
        _gate.Dispose();
    }
}
