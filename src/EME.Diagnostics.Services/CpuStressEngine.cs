using System.Diagnostics;
using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;

namespace EME.Diagnostics.Services;

public sealed class CpuStressEngine : ICpuStressEngine
{
    private int _running;
    private long _iterations;

    public bool IsRunning => Volatile.Read(ref _running) == 1;
    public event EventHandler<CpuStressMetrics>? MetricsUpdated;

    public async Task RunAsync(CpuStressOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.Duration));
        if (options.WorkerCount <= 0) throw new ArgumentOutOfRangeException(nameof(options.WorkerCount));
        if (Interlocked.Exchange(ref _running, 1) == 1) throw new InvalidOperationException("Um teste de CPU já está em execução.");

        Interlocked.Exchange(ref _iterations, 0);
        using var durationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var workers = Enumerable.Range(0, options.WorkerCount)
            .Select(index => Task.Run(() => RunWorker(index, durationCancellation.Token), durationCancellation.Token))
            .ToArray();

        try
        {
            using var metricsTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            while (stopwatch.Elapsed < options.Duration &&
                   await metricsTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                PublishMetrics(stopwatch.Elapsed, options, workers.Count(worker => !worker.IsCompleted));
            }

            PublishMetrics(options.Duration, options, workers.Count(worker => !worker.IsCompleted));
        }
        finally
        {
            durationCancellation.Cancel();
            try { await Task.WhenAll(workers).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            stopwatch.Stop();
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private void PublishMetrics(TimeSpan elapsed, CpuStressOptions options, int activeWorkers)
    {
        var progress = Math.Clamp(elapsed.TotalMilliseconds / options.Duration.TotalMilliseconds * 100d, 0d, 100d);
        MetricsUpdated?.Invoke(this, new CpuStressMetrics(
            elapsed > options.Duration ? options.Duration : elapsed,
            options.Duration,
            progress,
            activeWorkers,
            Interlocked.Read(ref _iterations)));
    }

    private void RunWorker(int workerIndex, CancellationToken cancellationToken)
    {
        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
        var value = 1.000001d + workerIndex * 0.000001d;
        while (!cancellationToken.IsCancellationRequested)
        {
            for (var iteration = 0; iteration < 25_000; iteration++)
            {
                value = Math.Sqrt(value * 1.0000001d + 0.0000001d);
                value = Math.Sin(value) * Math.Cos(value) + 1.5d;
            }
            Interlocked.Add(ref _iterations, 25_000);
        }

        GC.KeepAlive(value);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
