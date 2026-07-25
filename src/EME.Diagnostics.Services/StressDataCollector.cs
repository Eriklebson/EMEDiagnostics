using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;

namespace EME.Diagnostics.Services;

public sealed class StressDataCollector
{
    private readonly IReportRepository _repository;
    private ReportTestType _testType;
    private readonly List<HardwareSnapshot> _samples = [];

    public StressDataCollector(IReportRepository repository)
    {
        _repository = repository;
    }

    public void StartCollecting(ReportTestType testType)
    {
        _testType = testType;
        _samples.Clear();
    }

    public void AddSample(HardwareSnapshot snapshot)
    {
        _samples.Add(snapshot);
    }

    public int SampleCount => _samples.Count;

    public async Task<long> SaveReportAsync(TimeSpan elapsed, string status, CancellationToken ct = default)
    {
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

            var report = new StressReportDetail(
                0, DateTime.Now, _testType, elapsed, status,
                last.Cpu.Name, last.Gpu.Name,
                last.MemoryTotalGb,
                null,
                entries.AsReadOnly());

            return await _repository.SaveReportAsync(report, ct).ConfigureAwait(false);
        }

        return -1;
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
