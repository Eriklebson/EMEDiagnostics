using System.Diagnostics;
using System.Runtime.InteropServices;
using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;
using Microsoft.Win32.SafeHandles;

namespace EME.Diagnostics.Services;

public sealed class StorageStressEngine : IStorageStressEngine
{
    private int _running;
    private long _writeOps;
    private long _readOps;
    private long _totalBytesWritten;
    private long _totalBytesRead;

    private const int ChunkSize = 1024 * 1024;
    private const int ParallelStreams = 16;

    private const uint GenericRead = 0x80000000;
    private const uint FileShareRW = 0x00000003;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x80;
    private const uint FileFlagNoBuffering = 0x20000000;
    private const uint FileFlagSequentialScan = 0x08000000;

    private const uint MemCommit = 0x1000;
    private const uint PageReadWrite = 0x04;
    private const uint MemRelease = 0x8000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        nint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, nint hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(SafeFileHandle hFile, nint lpBuffer, uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead, nint lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualAlloc(nint lpAddress, nint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFree(nint lpAddress, nint dwSize, uint dwFreeType);

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

        Interlocked.Exchange(ref _writeOps, 0);
        Interlocked.Exchange(ref _readOps, 0);
        Interlocked.Exchange(ref _totalBytesWritten, 0);
        Interlocked.Exchange(ref _totalBytesRead, 0);

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var unlimited = options.Duration == Timeout.InfiniteTimeSpan;
        var filePathBase = Path.Combine(options.TargetDirectory, "eme_stress");
        var files = Enumerable.Range(0, ParallelStreams).Select(i => $"{filePathBase}_{i}.tmp").ToArray();
        var bytesPerStream = options.FileSizeMb * 1024L * 1024L / ParallelStreams;
        var chunksPerStream = (int)(bytesPerStream / ChunkSize);
        var fileSize = options.FileSizeMb * 1024L * 1024L;

        try
        {
            var ioTask = Task.Run(() =>
                IoLoop(files, bytesPerStream, chunksPerStream, fileSize, stopwatch, unlimited, options, cancellation.Token),
                cancellation.Token);

            using var metricsTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            while (await metricsTimer.WaitForNextTickAsync(cancellation.Token).ConfigureAwait(false))
            {
                PublishMetrics(stopwatch.Elapsed, options);
                if (ioTask.IsCompleted) break;
            }

            await ioTask.ConfigureAwait(false);
            PublishMetrics(stopwatch.Elapsed, options);
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

    private void IoLoop(string[] files, long bytesPerStream, int chunksPerStream, long fileSize,
        Stopwatch stopwatch, bool unlimited, StorageStressOptions options, CancellationToken ct)
    {
        bool writeMode = options.Mode == StorageTestMode.Write;

        if (writeMode)
        {
            // Write-only: create → write → close → repeat
            while (!ct.IsCancellationRequested && (unlimited || stopwatch.Elapsed < options.Duration))
            {
                var writeTasks = new Task[ParallelStreams];
                for (int fi = 0; fi < ParallelStreams; fi++)
                {
                    var filePath = files[fi];
                    writeTasks[fi] = Task.Run(() =>
                    {
                        if (ct.IsCancellationRequested) return;
                        WriteFile(filePath, bytesPerStream, chunksPerStream);
                    }, ct);
                }
                try { Task.WaitAll(writeTasks); }
                catch { break; }
                Interlocked.Add(ref _totalBytesWritten, fileSize);
            }
        }
        else
        {
            // Read-only: create files with data once, then read repeatedly with NO_BUFFERING
            for (int fi = 0; fi < ParallelStreams; fi++)
                WriteFile(files[fi], bytesPerStream, chunksPerStream);

            var readBuffers = new nint[ParallelStreams];
            for (int i = 0; i < ParallelStreams; i++)
                readBuffers[i] = VirtualAlloc(0, ChunkSize, MemCommit, PageReadWrite);

            try
            {
                while (!ct.IsCancellationRequested && (unlimited || stopwatch.Elapsed < options.Duration))
                {
                    var readHandles = new SafeFileHandle[ParallelStreams];
                    try
                    {
                        for (int fi = 0; fi < ParallelStreams; fi++)
                        {
                            readHandles[fi] = CreateFileW(files[fi], GenericRead, FileShareRW,
                                0, OpenExisting,
                                FileAttributeNormal | FileFlagNoBuffering | FileFlagSequentialScan, 0);
                        }

                        var readTasks = new Task[ParallelStreams];
                        for (int fi = 0; fi < ParallelStreams; fi++)
                        {
                            var handle = readHandles[fi];
                            if (handle == null || handle.IsInvalid) continue;
                            var buffer = readBuffers[fi];
                            readTasks[fi] = Task.Run(() => ReadRaw(handle, buffer, bytesPerStream, ct), ct);
                        }
                        try { Task.WaitAll(readTasks.Where(t => t != null).ToArray()); }
                        catch { break; }
                    }
                    finally
                    {
                        for (int fi = 0; fi < ParallelStreams; fi++)
                            readHandles[fi]?.Dispose();
                    }
                    Interlocked.Add(ref _totalBytesRead, fileSize);
                }
            }
            finally
            {
                for (int i = 0; i < ParallelStreams; i++)
                    if (readBuffers[i] != 0) VirtualFree(readBuffers[i], 0, MemRelease);
            }
        }
    }

    private void WriteFile(string filePath, long bytesToWrite, int chunksPerStream)
    {
        var buf = new byte[ChunkSize];
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write,
            FileShare.Read, ChunkSize,
            FileOptions.WriteThrough | FileOptions.SequentialScan);

        long remaining = bytesToWrite;
        while (remaining > 0)
        {
            Array.Fill(buf, (byte)(remaining & 0xFF));
            fs.Write(buf, 0, ChunkSize);
            remaining -= ChunkSize;
        }
        Interlocked.Add(ref _writeOps, chunksPerStream);
    }

    private void ReadRaw(SafeFileHandle handle, nint buffer, long bytesToRead, CancellationToken ct)
    {
        long remaining = bytesToRead;
        while (remaining > 0 && !ct.IsCancellationRequested)
        {
            var toRead = (uint)Math.Min(ChunkSize, remaining);
            uint bytesRead;
            if (!ReadFile(handle, buffer, toRead, out bytesRead, 0))
                break;
            if (bytesRead == 0) break;
            Interlocked.Add(ref _readOps, bytesRead);
            remaining -= bytesRead;
        }
    }

    private void PublishMetrics(TimeSpan elapsed, StorageStressOptions options)
    {
        var secs = elapsed.TotalSeconds;
        var written = Interlocked.Read(ref _totalBytesWritten);
        var read = Interlocked.Read(ref _totalBytesRead);
        var totalBytes = written + read;
        var throughput = secs > 0 ? totalBytes / secs / (1024d * 1024d) : 0;
        var progress = options.Duration != Timeout.InfiniteTimeSpan && options.Duration.TotalSeconds > 0
            ? Math.Clamp(elapsed.TotalMilliseconds / options.Duration.TotalMilliseconds * 100, 0, 100)
            : 0;

        MetricsUpdated?.Invoke(this, new StorageStressMetrics(
            elapsed,
            options.Duration,
            Math.Min(progress, 100),
            Math.Round(throughput, 1),
            Interlocked.Read(ref _writeOps) + Interlocked.Read(ref _readOps),
            0));
    }
}
