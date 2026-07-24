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
            var totalBytes = options.FileSizeMb * 1024L * 1024L;
            var buffer = new byte[64 * 1024];
            var totalWrites = (int)(totalBytes / buffer.Length);
            var completedOps = 0L;

            using var metricsTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            while ((unlimited || stopwatch.Elapsed < options.Duration) &&
                   await metricsTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                // Write phase
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.Asynchronous))
                {
                    for (int i = 0; i < totalWrites && (unlimited || stopwatch.Elapsed < options.Duration); i++)
                    {
                        Random.Shared.NextBytes(buffer);
                        await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
                        Interlocked.Increment(ref _operations);
                        if (cancellationToken.IsCancellationRequested) break;
                    }
                }

                completedOps++;
                PublishMetrics(stopwatch.Elapsed, options, completedOps, totalBytes);
                if (cancellationToken.IsCancellationRequested) break;

                // Read & verify phase
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None, buffer.Length, FileOptions.Asynchronous))
                {
                    var pos = 0L;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0 &&
                           (unlimited || stopwatch.Elapsed < options.Duration))
                    {
                        pos += read;
                        Interlocked.Increment(ref _operations);
                        if (cancellationToken.IsCancellationRequested) break;
                    }
                }

                completedOps++;
                PublishMetrics(stopwatch.Elapsed, options, completedOps, totalBytes);
                if (cancellationToken.IsCancellationRequested) break;
            }

            if (!unlimited)
                PublishMetrics(options.Duration, options, completedOps, totalBytes);
        }
        finally
        {
            durationCancellation.Cancel();
            stopwatch.Stop();
            try { if (File.Exists(filePath)) File.Delete(filePath); }
            catch { /* best effort */ }
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private void PublishMetrics(TimeSpan elapsed, StorageStressOptions options, long completedOps, long totalBytes)
    {
        var progress = totalBytes > 0 ? Math.Clamp(completedOps * 100.0 / Math.Max(1, (long)(options.FileSizeMb * 1024L * 1024L / (64 * 1024) * 2)), 0, 100) : 0;
        var elapsedSecs = elapsed.TotalSeconds;
        var throughput = elapsedSecs > 0 ? completedOps * 64L * 1024L / elapsedSecs / 1024d / 1024d : 0;
        MetricsUpdated?.Invoke(this, new StorageStressMetrics(
            elapsed,
            options.Duration,
            Math.Min(progress, 100),
            throughput,
            Interlocked.Read(ref _operations),
            Volatile.Read(ref _errors)));
    }
}
