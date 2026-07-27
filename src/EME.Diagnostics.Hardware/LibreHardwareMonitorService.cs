using System.Runtime.InteropServices;
using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;
using EME.HardwareDatabase.Services;
using LibreHardwareMonitor.Hardware;

namespace EME.Diagnostics.Hardware;

public sealed class LibreHardwareMonitorService : IHardwareMonitor
{
    private readonly Computer _computer;
    private readonly HardwareMappingResolver _mapping = new();
    private readonly CpuSensorMappingResolver _cpuMapping = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public LibreHardwareMonitorService()
    {
        EnsureHardwareDatabase();
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
        _cpuMapping.Load();
    }

    private static void EnsureHardwareDatabase()
    {
        try { new HardwareDatabaseUpdateService().EnsureHardwareDatabase(); }
        catch { }
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
            if (cpu != null) _cpuMapping.DetectCpu(cpu.Name);
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

            var (storageReadMBs, storageWriteMBs) = FindStorageThroughput(all);
            return new HardwareSnapshot(
                DateTimeOffset.Now,
                ToMetric(cpu, "CPU não detectada"),
                ToMetric(gpu, "GPU não detectada"),
                ReadMemory(memory, "Memory Used"),
                ReadMemory(memory, "Memory Available") + ReadMemory(memory, "Memory Used"),
                FindMemoryTemperature(all),
                FindStorageTemperature(all),
                FindStorageLoad(all),
                storageReadMBs,
                storageWriteMBs,
                fans,
                devices);
        }
        finally { _gate.Release(); }
    }

    private ComponentMetric ToMetric(IHardware? hardware, string fallback)
    {
        if (hardware is null) return new(fallback, null, null, null, null);
        var isCpu = hardware.HardwareType == HardwareType.Cpu;
        if (isCpu)
        {
            return new(
                hardware.Name,
                Find(hardware, SensorType.Load, "Total", "Core"),
                _cpuMapping.FindSensor(hardware, SensorType.Temperature,
                    _cpuMapping.GetTempSensorName() ?? "Core", _cpuMapping.GetTempFallbacks()),
                Find(hardware, SensorType.Clock, "Core", "GPU"),
                _cpuMapping.FindSensor(hardware, SensorType.Power,
                    _cpuMapping.GetPowerSensorName() ?? "Package", ["GPU"]));
        }
        return new(
            hardware.Name,
            Find(hardware, SensorType.Load, "Total", "Core"),
            Find(hardware, SensorType.Temperature, "Core", "Hot Spot"),
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

    private static double? FindStorageTemperature(IHardware[] all)
    {
        return all.Where(x => x.HardwareType == HardwareType.Storage)
            .SelectMany(x => x.Sensors)
            .FirstOrDefault(x => x.SensorType == SensorType.Temperature && x.Value.HasValue)?.Value;
    }

    private static double? FindStorageLoad(IHardware[] all)
    {
        var sensors = all.Where(x => x.HardwareType == HardwareType.Storage)
            .SelectMany(x => x.Sensors)
            .Where(x => x.SensorType == SensorType.Load && x.Value.HasValue)
            .ToArray();
        var activity = sensors.FirstOrDefault(s =>
            s.Name.Contains("Active", StringComparison.OrdinalIgnoreCase) ||
            s.Name.Contains("Activity", StringComparison.OrdinalIgnoreCase));
        if (activity?.Value.HasValue == true) return activity.Value;
        return sensors.FirstOrDefault(s =>
            !s.Name.Contains("Used", StringComparison.OrdinalIgnoreCase) &&
            !s.Name.Contains("Percentage", StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static (double? ReadMBs, double? WriteMBs) FindStorageThroughput(IHardware[] all)
    {
        var sensors = all.Where(x => x.HardwareType == HardwareType.Storage)
            .SelectMany(x => x.Sensors)
            .Where(x => x.SensorType == SensorType.Throughput && x.Value.HasValue)
            .ToArray();
        var read = sensors.FirstOrDefault(s => s.Name.Contains("Read", StringComparison.OrdinalIgnoreCase))?.Value;
        var write = sensors.FirstOrDefault(s => s.Name.Contains("Write", StringComparison.OrdinalIgnoreCase))?.Value;
        return (read / (1024.0 * 1024.0), write / (1024.0 * 1024.0));
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
