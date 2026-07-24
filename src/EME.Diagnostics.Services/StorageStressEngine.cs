using System.Diagnostics;
using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;

namespace EME.Diagnostics.Services;

public sealed class StorageStressEngine : IStorageStressEngine
{
    private int _running;
    private long _operations;
    private int _errors;
    private long _writeOps;
    private long _readOps;

    private const int ChunkSize = 64 * 1024;
    private const int ParallelStreams = 4;

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
        Interlocked.Exchange(ref _writeOps, 0);
        Interlocked.Exchange(ref _readOps, 0);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var unlimited = options.Duration == Timeout.InfiniteTimeSpan;
        var filePathBase = Path.Combine(options.TargetDirectory, "eme_stress");
        var files = Enumerable.Range(0, ParallelStreams).Select(i => $"{filePathBase}_{i}.tmp").ToArray();
        var totalChunks = options.FileSizeMb * 1024L * 1024L / ChunkSize;
        var chunkSizePerStream = totalChunks / ParallelStreams;

        try
        {
            // Start continuous I/O on background thread
            var ioTask = Task.Run(() =>
            {
                while (!cancellation.IsCancellationRequested &&
                       (unlimited || stopwatch.Elapsed < options.Duration))
                {
                    // Parallel write
                    Parallel.For(0, ParallelStreams, fi =>
                    {
                        var buf = new byte[ChunkSize];
                        using var fs = new FileStream(files[fi], FileMode.Create, FileAccess.Write, FileShare.Read, ChunkSize, FileOptions.SequentialScan);
                        for (int i = 0; i < chunkSizePerStream; i++)
                        {
                            Array.Fill(buf, (byte)(i & 0xFF));
                            fs.Write(buf, 0, ChunkSize);
                            Interlocked.Increment(ref _operations);
                        }
                        fs.Flush();
                    });
                    Interlocked.Add(ref _writeOps, totalChunks);

                    if (cancellation.IsCancellationRequested) break;

                    // Parallel read & verify
                    Parallel.For(0, ParallelStreams, fi =>
                    {
                        var buf = new byte[ChunkSize];
                        using var fs = new FileStream(files[fi], FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, FileOptions.SequentialScan);
                        int read, chunkIndex = 0;
                        while ((read = fs.Read(buf, 0, ChunkSize)) > 0)
                        {
                            for (int i = 0; i < read; i++)
                                if (buf[i] != (byte)(chunkIndex & 0xFF)) { Interlocked.Increment(ref _errors); break; }
                            chunkIndex++;
                            Interlocked.Increment(ref _operations);
                        }
                    });
                    Interlocked.Add(ref _readOps, totalChunks);
                }
            }, cancellation.Token);

            // Publish metrics on timer while I/O runs
            PublishMetrics(stopwatch.Elapsed, options);
            using var metricsTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            while (await metricsTimer.WaitForNextTickAsync(cancellation.Token).ConfigureAwait(false))
            {
                PublishMetrics(stopwatch.Elapsed, options);
                if (ioTask.IsCompleted) break;
            }

            await ioTask.ConfigureAwait(false);
            if (!unlimited)
                PublishMetrics(options.Duration, options);
        }
        finally
        {
            cancellation.Cancel();
            stopwatch.Stop();
            foreach (var f in files)
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private void PublishMetrics(TimeSpan elapsed, StorageStressOptions options)
    {
        var elapsedSecs = elapsed.TotalSeconds;
        var w = Interlocked.Read(ref _writeOps);
        var r = Interlocked.Read(ref _readOps);
        var totalOps = w + r;
        var totalExpected = options.FileSizeMb * 1024L * 1024L / ChunkSize * 2L;
        var progress = totalExpected > 0 ? Math.Clamp(totalOps * 100.0 / totalExpected, 0, 100) : 0;
        var throughput = elapsedSecs > 0 ? totalOps * ChunkSize / elapsedSecs / 1024d / 1024d : 0;
        MetricsUpdated?.Invoke(this, new StorageStressMetrics(
            elapsed,
            options.Duration,
            Math.Min(progress, 100),
            throughput,
            Interlocked.Read(ref _operations),
            Volatile.Read(ref _errors)));
    }
}
