using System.Runtime.InteropServices;
using System.Text;
using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;

namespace EME.Diagnostics.Services;

public sealed class DirectX11GpuStressEngine : IGpuStressEngine
{
    private const string LibraryName = "EME.Diagnostics.GpuEngine.dll";
    private int _running;

    public string BackendName => "DirectX 11 Compute";
    public bool IsAvailable
    {
        get
        {
            try { return NativeMethods.IsAvailable() != 0; }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }
    }

    public event EventHandler<GpuStressMetrics>? MetricsUpdated;

    public Task InitializeAsync(nint renderTargetHandle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAvailable) throw new NotSupportedException("O backend DirectX 11 não está disponível nesta máquina.");
        return Task.CompletedTask;
    }

    public async Task StartAsync(GpuStressOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.Duration));
        if (options.VramLimitPercent is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(options.VramLimitPercent));
        if (Interlocked.Exchange(ref _running, 1) == 1) throw new InvalidOperationException("Um teste de GPU já está em execução.");

        try
        {
            await InitializeAsync(0, cancellationToken).ConfigureAwait(false);
            if (NativeMethods.Start(options.Duration.TotalSeconds, options.Width, options.Height, options.TargetFps, options.VramLimitPercent, options.QualityLevel) == 0)
                throw new InvalidOperationException("O motor nativo recusou o início do teste de GPU.");

            using var registration = cancellationToken.Register(NativeMethods.Stop);
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                NativeMethods.GetMetrics(out var native);
                MetricsUpdated?.Invoke(this, native.ToManaged(options.Duration));
                if (native.IsRunning == 0)
                {
                    if (native.Errors > 0) throw new InvalidOperationException(GetNativeError());
                    break;
                }
            }
        }
        finally
        {
            NativeMethods.Stop();
            Interlocked.Exchange(ref _running, 0);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NativeMethods.Stop();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        NativeMethods.Shutdown();
        return ValueTask.CompletedTask;
    }

    private static string GetNativeError()
    {
        var buffer = new StringBuilder(512);
        NativeMethods.GetLastError(buffer, buffer.Capacity);
        return buffer.Length == 0 ? "Falha desconhecida no backend DirectX 11." : buffer.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeGpuMetrics
    {
        public double ElapsedSeconds;
        public double FramesPerSecond;
        public double FrameTimeMs;
        public double ProgressPercent;
        public ulong AllocatedVramBytes;
        public int Errors;
        public int IsRunning;

        public GpuStressMetrics ToManaged(TimeSpan duration) => new(
            TimeSpan.FromSeconds(ElapsedSeconds), duration, FramesPerSecond, FrameTimeMs,
            ProgressPercent, checked((long)AllocatedVramBytes), Errors);
    }

    private static class NativeMethods
    {
        [DllImport(LibraryName, EntryPoint = "EmeGpu_IsAvailable", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int IsAvailable();

        [DllImport(LibraryName, EntryPoint = "EmeGpu_Start", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Start(double durationSeconds, int width, int height, int targetFps, double vramLimitPercent, int qualityLevel);

        [DllImport(LibraryName, EntryPoint = "EmeGpu_Stop", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Stop();

        [DllImport(LibraryName, EntryPoint = "EmeGpu_GetMetrics", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void GetMetrics(out NativeGpuMetrics metrics);

        [DllImport(LibraryName, EntryPoint = "EmeGpu_GetLastError", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal static extern int GetLastError(StringBuilder buffer, int capacity);

        [DllImport(LibraryName, EntryPoint = "EmeGpu_Shutdown", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Shutdown();
    }
}
