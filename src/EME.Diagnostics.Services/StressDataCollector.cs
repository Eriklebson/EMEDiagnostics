using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;

namespace EME.Diagnostics.Services;

public sealed class StressDataCollector
{
    private readonly IReportRepository _repository;
    private ReportTestType _testType;
    private bool _isCollecting;
    private readonly List<HardwareSnapshot> _samples = [];

    public StressDataCollector(IReportRepository repository)
    {
        _repository = repository;
    }

    public void StartCollecting(ReportTestType testType)
    {
        _testType = testType;
        _isCollecting = true;
        _samples.Clear();
    }

    public bool IsCollecting => _isCollecting;

    public void AddSample(HardwareSnapshot snapshot)
    {
        _samples.Add(snapshot);
    }

    public async Task<long> SaveReportAsync(TimeSpan elapsed, string status, CancellationToken ct = default)
    {
        _isCollecting = false;
        var entries = new List<ReportEntry>();

        if (_samples.Count > 0)
        {
            var last = _samples[^1];

            AddAggregated(entries, "CPU", [
                ("Uso (%)", s => s.Cpu.Usage, "%"),
                ("Temperatura (°C)", s => s.Cpu.Temperature, "°C"),
                ("Clock (MHz)", s => s.Cpu.Clock, "MHz"),
                ("Potência (W)", s => s.Cpu.Power, "W")
            ]);

            AddAggregated(entries, "GPU", [
                ("Uso (%)", s => s.Gpu.Usage, "%"),
                ("Temperatura (°C)", s => s.Gpu.Temperature, "°C"),
                ("Clock (MHz)", s => s.Gpu.Clock, "MHz"),
                ("Potência (W)", s => s.Gpu.Power, "W")
            ]);

            AddAggregated(entries, "RAM", [
                ("Uso (%)", s => s.MemoryTotalGb > 0 ? s.MemoryUsedGb / s.MemoryTotalGb * 100 : null, "%"),
                ("Usada (GB)", s => s.MemoryUsedGb, "GB"),
                ("Temperatura (°C)", s => s.MemoryTemperature, "°C")
            ]);

            AddAggregated(entries, "Storage", [
                ("Temperatura (°C)", s => s.StorageTemperature, "°C"),
                ("Leitura (MB/s)", s => s.StorageReadMBs, "MB/s"),
                ("Escrita (MB/s)", s => s.StorageWriteMBs, "MB/s")
            ]);

            if (_samples[0].Fans.Count > 0)
                foreach (var fan in _samples[0].Fans)
                    AddAggregated(entries, fan.Category, [($"{fan.Name} (RPM)", s => (double?)s.Fans.FirstOrDefault(f => f.Name == fan.Name)?.Rpm, "RPM")]);

            var result = ComputeThrottlingResult();

            var report = new StressReportDetail(
                0, DateTime.Now, _testType, elapsed, status,
                last.Cpu.Name, last.Gpu.Name,
                last.MemoryTotalGb,
                null,
                entries.AsReadOnly(),
                result);

            return await _repository.SaveReportAsync(report, ct).ConfigureAwait(false);
        }

        return -1;
    }

    private string ComputeThrottlingResult()
    {
        if (_samples.Count < 3)
            return "Pendente";

        var cpuClocks = _samples.Select(s => s.Cpu.Clock).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var gpuClocks = _samples.Select(s => s.Gpu.Clock).Where(v => v.HasValue).Select(v => v!.Value).ToList();

        bool cpuThrottle = DetectThrottling(cpuClocks);
        bool gpuThrottle = DetectThrottling(gpuClocks);

        if (cpuThrottle && gpuThrottle) return "RECUSADO CPU+GPU";
        if (cpuThrottle) return "RECUSADO CPU";
        if (gpuThrottle) return "RECUSADO GPU";
        return "PASS";
    }

    private static bool DetectThrottling(List<double> clocks)
    {
        if (clocks.Count < 5) return false;

        var maxClock = clocks.Max();
        if (maxClock <= 0) return false;

        var firstHalf = clocks.Take(clocks.Count / 2).Average();
        var secondHalf = clocks.Skip(clocks.Count / 2).Average();

        return secondHalf < firstHalf * 0.75 && firstHalf > 100;
    }

    private void AddAggregated(List<ReportEntry> entries, string component, (string Name, Func<HardwareSnapshot, double?> Selector, string Unit)[] sensors)
    {
        foreach (var (name, selector, unit) in sensors)
        {
            var values = _samples.Select(selector).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            entries.Add(new ReportEntry(
                component, name, unit,
                values.Count > 0 ? values.Min() : null,
                values.Count > 0 ? values.Max() : null,
                values.Count > 0 ? values.Average() : null));
        }
    }
}
