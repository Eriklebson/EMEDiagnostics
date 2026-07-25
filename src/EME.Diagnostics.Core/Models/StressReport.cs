namespace EME.Diagnostics.Core.Models;

public enum ReportTestType { Cpu, Gpu, Memory, Storage, Combined }

public sealed record StressReportSummary(
    long Id,
    DateTime CreatedAt,
    ReportTestType TestType,
    TimeSpan Duration,
    string Status,
    int EntryCount);

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
    IReadOnlyList<ReportEntry> Entries);

public sealed record ReportEntry(
    string Component,
    string SensorName,
    string Unit,
    double? MinValue,
    double? MaxValue,
    double? AvgValue);
