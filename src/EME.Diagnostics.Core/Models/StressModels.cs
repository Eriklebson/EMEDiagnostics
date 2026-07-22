namespace EME.Diagnostics.Core.Models;

public enum StressTarget { Cpu, Gpu, Memory, Storage, Combined }
public enum StressStatus { NotStarted, Running, Completed, Failed, Cancelled }

public sealed record StressTestDefinition(StressTarget Target, string Title, string Description, TimeSpan DefaultDuration);
public sealed record CpuStressOptions(TimeSpan Duration, int WorkerCount);
public sealed record CpuStressMetrics(TimeSpan Elapsed, TimeSpan Duration, double ProgressPercent, int ActiveWorkers, long Iterations);
public sealed record GpuStressOptions(TimeSpan Duration, int Width, int Height, int TargetFps, double VramLimitPercent);
public sealed record GpuStressMetrics(double FramesPerSecond, double FrameTimeMs, double ProgressPercent, long AllocatedVramBytes, int Errors);
