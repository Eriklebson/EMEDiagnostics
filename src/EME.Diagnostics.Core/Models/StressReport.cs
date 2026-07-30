namespace EME.Diagnostics.Core.Models;

public enum ReportTestType { Cpu, Gpu, Memory, Storage, Combined }

public enum StressTestResult { Pending, Pass, RecusadoCpu, RecusadoGpu, RecusadoCpuGpu }

public sealed record StressReportSummary(
    long Id,
    DateTime CreatedAt,
    ReportTestType TestType,
    TimeSpan Duration,
    string Status,
    int EntryCount,
    string Result = "Pendente");

public sealed record StressReportDetail(
    long Id,
    DateTime CreatedAt,
    ReportTestType TestType,
    TimeSpan Duration,
    string Status,
    string? CpuName,
    string? GpuName,
    double MemoryTotalGb,
    string? StorageName,
    IReadOnlyList<ReportEntry> Entries,
    string Result = "Pendente");

public sealed record ReportEntry(
    string Component,
    string SensorName,
    string Unit,
    double? MinValue,
    double? MaxValue,
    double? AvgValue);
