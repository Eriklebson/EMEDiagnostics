using System.Diagnostics;
using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;

namespace EME.Diagnostics.Services;

public sealed class StorageStressEngine : IStorageStressEngine
{
    private int _running;
    private long _operations;
    private int _errors;

    public bool IsRunning => Volatile.Read(ref _running) == 1;
    public event EventHandler<StorageStressMetrics>? MetricsUpdated;

    public async Task RunAsync(StorageStressOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Duration <= TimeSpan.Zero && options.Duration != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(options.Duration));
        if (options.FileSizeMb is < 16 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(options.FileSizeMb));
        if (Interlocked.Exchange(ref _running, 1) == 1)
            throw new InvalidOperationException("Um teste de armazenamento já está em execução.");

        Interlocked.Exchange(ref _operations, 0);
        Interlocked.Exchange(ref _errors, 0);
        using var durationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var unlimited = options.Duration == Timeout.InfiniteTimeSpan;
        var filePath = Path.Combine(options.TargetDirectory, "eme_stress.tmp");

        try
        {
            var buffer = new byte[64 * 1024];
            var totalChunks = options.FileSizeMb * 1024L * 1024L / buffer.Length;
            long writeOps = 0, readOps = 0;

            PublishMetrics(stopwatch.Elapsed, options, 0, 0);
            using var metricsTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            while ((unlimited || stopwatch.Elapsed < options.Duration) &&
                   await metricsTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                // Write phase
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, buffer.Length, FileOptions.SequentialScan))
                {
                    for (int i = 0; i < totalChunks && (unlimited || stopwatch.Elapsed < options.Duration); i++)
                    {
                        Array.Fill(buffer, (byte)i);
                        await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
                        Interlocked.Increment(ref _operations);
                        if (cancellationToken.IsCancellationRequested) break;
                    }
                }
                writeOps += totalChunks;

                // Sync before read
                using (var fsync = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan))
                {
                    fsync.Flush();
                }

                PublishMetrics(stopwatch.Elapsed, options, writeOps, readOps);
                if (cancellationToken.IsCancellationRequested) break;

                // Read & verify phase
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.SequentialScan))
                {
                    var expected = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0 &&
                           (unlimited || stopwatch.Elapsed < options.Duration))
                    {
                        for (int i = 0; i < read; i++)
                        {
                            if (buffer[i] != (byte)expected)
                            {
                                Interlocked.Increment(ref _errors);
                                break;
                            }
                        }
                        expected++;
                        Interlocked.Increment(ref _operations);
                        if (cancellationToken.IsCancellationRequested) break;
                    }
                }
                readOps += totalChunks;

                PublishMetrics(stopwatch.Elapsed, options, writeOps, readOps);
                if (cancellationToken.IsCancellationRequested) break;
            }

            if (!unlimited)
                PublishMetrics(options.Duration, options, writeOps, readOps);
        }
        finally
        {
            durationCancellation.Cancel();
            stopwatch.Stop();
            try { if (File.Exists(filePath)) File.Delete(filePath); }
            catch { }
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private void PublishMetrics(TimeSpan elapsed, StorageStressOptions options, long writeOps, long readOps)
    {
        var elapsedSecs = elapsed.TotalSeconds;
        var totalOps = writeOps + readOps;
        var totalExpected = options.FileSizeMb * 1024L * 1024L / (64 * 1024) * 2L;
        var progress = totalExpected > 0 ? Math.Clamp(totalOps * 100.0 / totalExpected, 0, 100) : 0;
        var throughput = elapsedSecs > 0 ? totalOps * 64L * 1024L / elapsedSecs / 1024d / 1024d : 0;
        MetricsUpdated?.Invoke(this, new StorageStressMetrics(
            elapsed,
            options.Duration,
            Math.Min(progress, 100),
            throughput,
            Interlocked.Read(ref _operations),
            Volatile.Read(ref _errors)));
    }
}
