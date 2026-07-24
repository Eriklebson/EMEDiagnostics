using System.Diagnostics;
using System.Runtime.InteropServices;
using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;

namespace EME.Diagnostics.Services;

public sealed class MemoryStressEngine : IMemoryStressEngine
{
    private int _running;
    private long _operations;
    private int _errors;

    private const int ChunkSizeMb = 256;

    public bool IsRunning => Volatile.Read(ref _running) == 1;
    public event EventHandler<MemoryStressMetrics>? MetricsUpdated;

    public async Task RunAsync(MemoryStressOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Duration <= TimeSpan.Zero && options.Duration != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(options.Duration));
        if (options.SizeMegabytes is < 64 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(options.SizeMegabytes));
        if (Interlocked.Exchange(ref _running, 1) == 1)
            throw new InvalidOperationException("Um teste de RAM já está em execução.");

        Interlocked.Exchange(ref _operations, 0);
        Interlocked.Exchange(ref _errors, 0);
        using var durationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var unlimited = options.Duration == Timeout.InfiniteTimeSpan;

        try
        {
            var totalMb = options.SizeMegabytes;
            var numChunks = (totalMb + ChunkSizeMb - 1) / ChunkSizeMb;
            var chunks = new byte[numChunks][];
            for (int i = 0; i < numChunks; i++)
            {
                var size = Math.Min(ChunkSizeMb, totalMb - i * ChunkSizeMb);
                chunks[i] = new byte[size * 1024L * 1024L];
            }

            var patterns = new byte[] { 0xAA, 0x55, 0xFF, 0x00, 0x69 };
            var patternCount = patterns.Length;
            var completedPatterns = 0L;

            using var metricsTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            while ((unlimited || stopwatch.Elapsed < options.Duration) &&
                   await metricsTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                for (int p = 0; p < patternCount && (unlimited || stopwatch.Elapsed < options.Duration); p++)
                {
                    var pattern = patterns[p];
                    var parallelOpts = new ParallelOptions
                    {
                        CancellationToken = cancellationToken,
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    };

                    Parallel.For(0, numChunks, parallelOpts, chunkIndex =>
                    {
                        var data = chunks[chunkIndex];
                        Array.Fill(data, pattern);
                        Interlocked.Increment(ref _operations);

                        for (int i = 0; i < data.Length; i++)
                        {
                            if (data[i] != pattern)
                            {
                                Interlocked.Increment(ref _errors);
                                break;
                            }
                        }
                    });

                    completedPatterns++;
                    PublishMetrics(stopwatch.Elapsed, options, completedPatterns, patternCount, unlimited);
                    if (cancellationToken.IsCancellationRequested) break;
                }
            }

            if (!unlimited)
                PublishMetrics(options.Duration, options, patternCount, patternCount, false);
        }
        finally
        {
            durationCancellation.Cancel();
            stopwatch.Stop();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            TrimWorkingSet();
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private static void TrimWorkingSet()
    {
        try
        {
            var handle = Process.GetCurrentProcess().Handle;
            SetProcessWorkingSetSize(handle, -1, -1);
        }
        catch { }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int SetProcessWorkingSetSize(IntPtr hProcess, int dwMinimumWorkingSetSize, int dwMaximumWorkingSetSize);

    private void PublishMetrics(TimeSpan elapsed, MemoryStressOptions options, long completedPatterns, int totalPatterns, bool unlimited)
    {
        var progress = Math.Clamp(completedPatterns * 100.0 / totalPatterns, 0, 100);
        var effectiveDuration = unlimited ? elapsed : (elapsed > options.Duration ? options.Duration : elapsed);
        MetricsUpdated?.Invoke(this, new MemoryStressMetrics(
            effectiveDuration,
            options.Duration,
            progress,
            options.SizeMegabytes,
            Interlocked.Read(ref _operations),
            Volatile.Read(ref _errors)));
    }
}
