namespace EME.Diagnostics.Core.Models;

public sealed record ComponentMetric(string Name, double? Usage, double? Temperature, double? Clock, double? Power);

public sealed record FanMetric(string Name, double Rpm, string Category = "Unknown");

public sealed record SensorMetric(
    string Name,
    string Type,
    double? Value,
    double? Minimum,
    double? Maximum,
    string Unit,
    string Identifier);

public sealed record HardwareDeviceSnapshot(
    string Name,
    string Type,
    string Identifier,
    string? ParentName,
    IReadOnlyList<SensorMetric> Sensors);

public sealed record HardwareSnapshot(
    DateTimeOffset CapturedAt,
    ComponentMetric Cpu,
    ComponentMetric Gpu,
    double MemoryUsedGb,
    double MemoryTotalGb,
    double? MemoryTemperature,
    double? StorageTemperature,
    double? StorageLoad,
    IReadOnlyList<FanMetric> Fans,
    IReadOnlyList<HardwareDeviceSnapshot> Devices)
{
    public static HardwareSnapshot Empty { get; } = new(
        DateTimeOffset.Now,
        new("CPU não detectada", null, null, null, null),
        new("GPU não detectada", null, null, null, null),
        0,
        0,
        null,
        null,
        null,
        Array.Empty<FanMetric>(),
        Array.Empty<HardwareDeviceSnapshot>());
}
