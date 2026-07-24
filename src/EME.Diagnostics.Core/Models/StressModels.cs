namespace EME.Diagnostics.Core.Models;

public enum StressTarget { Cpu, Gpu, Memory, Storage, Combined }
public enum StressStatus { NotStarted, Running, Completed, Failed, Cancelled, Cancelling }

public sealed record StressTestDefinition(StressTarget Target, string Title, string Description, TimeSpan DefaultDuration);
public sealed record CpuStressOptions(TimeSpan Duration, int WorkerCount);
public sealed record CpuStressMetrics(TimeSpan Elapsed, TimeSpan Duration, double ProgressPercent, int ActiveWorkers, long Iterations);
public sealed record GpuStressOptions(TimeSpan Duration, int Width, int Height, int TargetFps, double VramLimitPercent, int QualityLevel = 2);
public sealed record GpuStressMetrics(TimeSpan Elapsed, TimeSpan Duration, double FramesPerSecond, double FrameTimeMs, double ProgressPercent, long AllocatedVramBytes, int Errors);
public sealed record VramTestMetrics(TimeSpan Elapsed, double ProgressPercent, long BytesTested, long TotalBytes, int Errors);
public sealed record MemoryStressOptions(TimeSpan Duration, int SizeMegabytes);
public sealed record MemoryStressMetrics(TimeSpan Elapsed, TimeSpan Duration, double ProgressPercent, int AllocatedMb, long Operations, int Errors);
public enum StorageTestMode { Write, Read }
public sealed record StorageStressOptions(TimeSpan Duration, int FileSizeMb, string TargetDirectory, StorageTestMode Mode = StorageTestMode.Write);
public sealed record StorageStressMetrics(TimeSpan Elapsed, TimeSpan Duration, double ProgressPercent, double ThroughputMBs, long Operations, int Errors);
