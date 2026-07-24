using System.Runtime.InteropServices;
using EME.Diagnostics.Core.Models;

namespace EME.Diagnostics.Services;

public sealed class GpuVramTest : IDisposable
{
    private const string LibraryName = "EME.Diagnostics.GpuEngine.dll";
    private int _running;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromMilliseconds(250));

    public event EventHandler<VramTestMetrics>? MetricsUpdated;

    public bool IsAvailable
    {
        get
        {
            try { return NativeMethods.IsAvailable() != 0; }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }
    }

    public async Task RunTestAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
            throw new InvalidOperationException("Um teste de VRAM já está em execução.");

        try
        {
            if (NativeMethods.VramTestStart() == 0)
                throw new InvalidOperationException("O motor nativo recusou o início do teste de VRAM.");

            using var registration = cancellationToken.Register(NativeMethods.VramTestStop);
            while (await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                NativeMethods.VramTestGetMetrics(out var native);
                MetricsUpdated?.Invoke(this, native.ToManaged());
                if (native.IsRunning == 0)
                {
                    if (native.Errors > 0)
                        throw new InvalidOperationException($"Teste de VRAM concluído com {native.Errors} erro(s).");
                    break;
                }
            }
        }
        finally
        {
            NativeMethods.VramTestStop();
            Interlocked.Exchange(ref _running, 0);
        }
    }

    public void Stop()
    {
        NativeMethods.VramTestStop();
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeVramMetrics
    {
        public double ElapsedSeconds;
        public double ProgressPercent;
        public ulong BytesTested;
        public ulong TotalBytes;
        public int Errors;
        public int IsRunning;

        public VramTestMetrics ToManaged() => new(
            TimeSpan.FromSeconds(ElapsedSeconds), ProgressPercent,
            checked((long)BytesTested), checked((long)TotalBytes), Errors);
    }

    private static class NativeMethods
    {
        [DllImport(LibraryName, EntryPoint = "EmeGpu_IsAvailable", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int IsAvailable();

        [DllImport(LibraryName, EntryPoint = "EmeGpu_VramTest_Start", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int VramTestStart();

        [DllImport(LibraryName, EntryPoint = "EmeGpu_VramTest_Stop", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void VramTestStop();

        [DllImport(LibraryName, EntryPoint = "EmeGpu_VramTest_GetMetrics", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void VramTestGetMetrics(out NativeVramMetrics metrics);
    }
}
