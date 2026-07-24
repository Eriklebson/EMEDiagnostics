using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;
using EME.Diagnostics.Services;

namespace EME.Diagnostics.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IHardwareMonitor _hardware;
    private readonly ICpuStressEngine _cpuStressEngine;
    private readonly IGpuStressEngine _gpuStressEngine;
    private readonly IMemoryStressEngine _memoryStressEngine;
    private readonly IStorageStressEngine _storageStressEngine;
    private readonly GpuVramTest _vramTest = new();
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(1));
    private readonly CancellationTokenSource _cancellation = new();
    private CancellationTokenSource? _cpuStressCancellation;
    private CancellationTokenSource? _gpuStressCancellation;
    private CancellationTokenSource? _memoryStressCancellation;
    private CancellationTokenSource? _storageStressCancellation;
    private CancellationTokenSource? _vramTestCancellation;

    private string _currentPage = "Dashboard";
    private HardwareSnapshot _snapshot = HardwareSnapshot.Empty;
    private string _status = "Inicializando sensores...";
    private CpuStressMetrics? _cpuStressMetrics;
    private StressStatus _cpuStressStatus = StressStatus.NotStarted;
    private GpuStressMetrics? _gpuStressMetrics;
    private StressStatus _gpuStressStatus = StressStatus.NotStarted;
    private VramTestMetrics? _vramTestMetrics;
    private StressStatus _vramTestStatus = StressStatus.NotStarted;
    private MemoryStressMetrics? _memoryStressMetrics;
    private StressStatus _memoryStressStatus = StressStatus.NotStarted;
    private StorageStressMetrics? _storageStressMetrics;
    private StressStatus _storageStressStatus = StressStatus.NotStarted;

    public string CurrentPage { get => _currentPage; private set => SetProperty(ref _currentPage, value); }
    public HardwareSnapshot Snapshot { get => _snapshot; private set => SetProperty(ref _snapshot, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public CpuStressMetrics? CpuStressMetrics { get => _cpuStressMetrics; private set => SetProperty(ref _cpuStressMetrics, value); }
    public StressStatus CpuStressStatus { get => _cpuStressStatus; private set => SetProperty(ref _cpuStressStatus, value); }
    public GpuStressMetrics? GpuStressMetrics { get => _gpuStressMetrics; private set => SetProperty(ref _gpuStressMetrics, value); }
    public StressStatus GpuStressStatus { get => _gpuStressStatus; private set => SetProperty(ref _gpuStressStatus, value); }
    public VramTestMetrics? VramTestMetrics { get => _vramTestMetrics; private set => SetProperty(ref _vramTestMetrics, value); }
    public StressStatus VramTestStatus { get => _vramTestStatus; private set => SetProperty(ref _vramTestStatus, value); }
    public MemoryStressMetrics? MemoryStressMetrics { get => _memoryStressMetrics; private set => SetProperty(ref _memoryStressMetrics, value); }
    public StressStatus MemoryStressStatus { get => _memoryStressStatus; private set => SetProperty(ref _memoryStressStatus, value); }
    public StorageStressMetrics? StorageStressMetrics { get => _storageStressMetrics; private set => SetProperty(ref _storageStressMetrics, value); }
    public StressStatus StorageStressStatus { get => _storageStressStatus; private set => SetProperty(ref _storageStressStatus, value); }
    public string GpuBackendName => _gpuStressEngine.BackendName;
    public bool IsGpuStressAvailable => _gpuStressEngine.IsAvailable;
    public bool IsVramTestAvailable => _vramTest.IsAvailable;

    public MainViewModel(IHardwareMonitor hardware, ICpuStressEngine cpuStressEngine, IGpuStressEngine gpuStressEngine, IMemoryStressEngine memoryStressEngine, IStorageStressEngine storageStressEngine)
    {
        _hardware = hardware;
        _cpuStressEngine = cpuStressEngine;
        _gpuStressEngine = gpuStressEngine;
        _memoryStressEngine = memoryStressEngine;
        _storageStressEngine = storageStressEngine;
        _cpuStressEngine.MetricsUpdated += OnCpuStressMetricsUpdated;
        _gpuStressEngine.MetricsUpdated += OnGpuStressMetricsUpdated;
        _memoryStressEngine.MetricsUpdated += OnMemoryStressMetricsUpdated;
        _storageStressEngine.MetricsUpdated += OnStorageStressMetricsUpdated;
        _vramTest.MetricsUpdated += OnVramTestMetricsUpdated;
    }

    public async Task StartAsync()
    {
        await RefreshAsync();
        _ = RefreshLoopAsync(_cancellation.Token);
    }

    [RelayCommand]
    private void Navigate(string? page)
    {
        if (!string.IsNullOrWhiteSpace(page)) CurrentPage = page;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var snapshot = await _hardware.CaptureAsync(_cancellation.Token);
            var mem = GetMemoryStatus();
            var usedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
            var totalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
            Snapshot = snapshot with { MemoryUsedGb = usedGb, MemoryTotalGb = totalGb, MemoryTemperature = snapshot.MemoryTemperature, StorageTemperature = snapshot.StorageTemperature, StorageLoad = snapshot.StorageLoad };
            Status = $"Dados atualizados às {Snapshot.CapturedAt:HH:mm:ss}";
        }
        catch (Exception ex) { Status = $"Sensores indisponíveis: {ex.Message}"; }
    }

    public async Task StartCpuStressAsync(TimeSpan duration)
    {
        if (CpuStressStatus == StressStatus.Running) return;

        _cpuStressCancellation?.Dispose();
        _cpuStressCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
        CpuStressMetrics = new CpuStressMetrics(TimeSpan.Zero, duration, 0, Environment.ProcessorCount, 0);
        CpuStressStatus = StressStatus.Running;
        Status = $"Teste de CPU iniciado com {Environment.ProcessorCount} workers.";

        try
        {
            await _cpuStressEngine.RunAsync(
                new CpuStressOptions(duration, Environment.ProcessorCount),
                _cpuStressCancellation.Token);
            CpuStressStatus = StressStatus.Completed;
            Status = "Teste de CPU concluído com sucesso.";
        }
        catch (OperationCanceledException)
        {
            CpuStressStatus = StressStatus.Cancelled;
            Status = "Teste de CPU cancelado.";
        }
        catch (Exception exception)
        {
            CpuStressStatus = StressStatus.Failed;
            Status = $"Falha no teste de CPU: {exception.Message}";
        }
    }

    public void StopCpuStress() => _cpuStressCancellation?.Cancel();

    public async Task StartGpuStressAsync(TimeSpan duration, int qualityLevel = 2)
    {
        if (GpuStressStatus == StressStatus.Running) return;

        _gpuStressCancellation?.Dispose();
        _gpuStressCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
        GpuStressMetrics = new GpuStressMetrics(TimeSpan.Zero, duration, 0, 0, 0, 0, 0);
        GpuStressStatus = StressStatus.Running;
        var qualityNames = new[] { "Baixa", "Média", "Alta", "Ultra" };
        var qualityName = qualityNames[Math.Clamp(qualityLevel, 0, 3)];
        Status = $"Teste de GPU iniciado ({qualityName}) com {_gpuStressEngine.BackendName} — modo contínuo.";

        try
        {
            await _gpuStressEngine.StartAsync(new GpuStressOptions(duration, 1600, 900, 0, 15, qualityLevel), _gpuStressCancellation.Token);
            GpuStressStatus = StressStatus.Completed;
            Status = "Teste de GPU concluído com sucesso.";
        }
        catch (OperationCanceledException)
        {
            GpuStressStatus = StressStatus.Cancelled;
            Status = Snapshot.Gpu.Temperature is >= 90
                ? "Teste de GPU interrompido pela proteção térmica (90 °C)."
                : "Teste de GPU cancelado.";
        }
        catch (Exception exception)
        {
            GpuStressStatus = StressStatus.Failed;
            Status = $"Falha no teste de GPU: {exception.Message}";
        }
    }

    public void StopGpuStress()
    {
        GpuStressStatus = StressStatus.Cancelling;
        Status = "Cancelando... aguardando parada do motor gráfico.";
        _gpuStressCancellation?.Cancel();
    }

    public async Task StartVramTestAsync()
    {
        if (VramTestStatus == StressStatus.Running) return;

        _vramTestCancellation?.Dispose();
        _vramTestCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
        VramTestMetrics = new VramTestMetrics(TimeSpan.Zero, 0, 0, 0, 0);
        VramTestStatus = StressStatus.Running;
        Status = "Teste de VRAM iniciado — escrevendo e verificando padrões na memória de vídeo...";

        try
        {
            await _vramTest.RunTestAsync(_vramTestCancellation.Token);
            VramTestStatus = StressStatus.Completed;
            Status = "Teste de VRAM concluído sem erros.";
        }
        catch (OperationCanceledException)
        {
            VramTestStatus = StressStatus.Cancelled;
            Status = "Teste de VRAM cancelado.";
        }
        catch (Exception exception)
        {
            VramTestStatus = exception.Message.Contains("erro(s)")
                ? StressStatus.Failed
                : StressStatus.Failed;
            Status = $"Falha no teste de VRAM: {exception.Message}";
        }
    }

    public void StopVramTest()
    {
        VramTestStatus = StressStatus.Cancelling;
        Status = "Cancelando teste de VRAM...";
        _vramTestCancellation?.Cancel();
        _vramTest.Stop();
    }

    public async Task StartMemoryStressAsync(TimeSpan duration, int sizeMb = 0)
    {
        if (MemoryStressStatus == StressStatus.Running) return;

        if (sizeMb <= 0) sizeMb = GetDefaultMemorySize();
        _memoryStressCancellation?.Dispose();
        _memoryStressCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
        MemoryStressMetrics = new MemoryStressMetrics(TimeSpan.Zero, duration, 0, sizeMb, 0, 0);
        MemoryStressStatus = StressStatus.Running;
        Status = $"Teste de RAM iniciado — {sizeMb} MB com {Environment.ProcessorCount} threads.";

        try
        {
            await _memoryStressEngine.RunAsync(
                new MemoryStressOptions(duration, sizeMb),
                _memoryStressCancellation.Token);
            MemoryStressStatus = StressStatus.Completed;
            Status = "Teste de RAM concluído com sucesso.";
        }
        catch (OperationCanceledException)
        {
            MemoryStressStatus = StressStatus.Cancelled;
            Status = "Teste de RAM cancelado.";
        }
        catch (Exception exception)
        {
            MemoryStressStatus = StressStatus.Failed;
            Status = $"Falha no teste de RAM: {exception.Message}";
        }
    }

    public void StopMemoryStress()
    {
        MemoryStressStatus = StressStatus.Cancelling;
        Status = "Cancelando teste de RAM...";
        _memoryStressCancellation?.Cancel();
    }

    public async Task StartStorageStressAsync(TimeSpan duration)
    {
        if (StorageStressStatus == StressStatus.Running) return;

        _storageStressCancellation?.Dispose();
        _storageStressCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
        StorageStressMetrics = new StorageStressMetrics(TimeSpan.Zero, duration, 0, 0, 0, 0);
        StorageStressStatus = StressStatus.Running;
        Status = $"Teste de Storage iniciado — {256} MB em lotes de 64 KB.";

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "EMEDiagnostics");
            Directory.CreateDirectory(tempDir);
            await _storageStressEngine.RunAsync(
                new StorageStressOptions(duration, 256, tempDir),
                _storageStressCancellation.Token);
            StorageStressStatus = StressStatus.Completed;
            Status = "Teste de Storage concluído com sucesso.";
        }
        catch (OperationCanceledException)
        {
            StorageStressStatus = StressStatus.Cancelled;
            Status = "Teste de Storage cancelado.";
        }
        catch (Exception exception)
        {
            StorageStressStatus = StressStatus.Failed;
            Status = $"Falha no teste de Storage: {exception.Message}";
        }
    }

    public void StopStorageStress()
    {
        StorageStressStatus = StressStatus.Cancelling;
        Status = "Cancelando teste de Storage...";
        _storageStressCancellation?.Cancel();
    }

    private static MEMORYSTATUSEX GetMemoryStatus()
    {
        var mem = new MEMORYSTATUSEX();
        mem.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>();
        GlobalMemoryStatusEx(ref mem);
        return mem;
    }

    private static int GetDefaultMemorySize()
    {
        var mem = GetMemoryStatus();
        return (int)Math.Clamp((long)(mem.ullAvailPhys / (1024 * 1024)), 64, 65536);
    }

    [System.Runtime.InteropServices.LibraryImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    private void OnCpuStressMetricsUpdated(object? sender, CpuStressMetrics metrics) => CpuStressMetrics = metrics;
    private void OnGpuStressMetricsUpdated(object? sender, GpuStressMetrics metrics) => GpuStressMetrics = metrics;
    private void OnMemoryStressMetricsUpdated(object? sender, MemoryStressMetrics metrics) => MemoryStressMetrics = metrics;
    private void OnStorageStressMetricsUpdated(object? sender, StorageStressMetrics metrics) => StorageStressMetrics = metrics;
    private void OnVramTestMetricsUpdated(object? sender, VramTestMetrics metrics) => VramTestMetrics = metrics;

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshAsync();
                if (GpuStressStatus == StressStatus.Running && Snapshot.Gpu.Temperature is >= 90)
                    _gpuStressCancellation?.Cancel();
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _cpuStressCancellation?.Cancel();
        _gpuStressCancellation?.Cancel();
        _memoryStressCancellation?.Cancel();
        _storageStressCancellation?.Cancel();
        _vramTestCancellation?.Cancel();
        _cpuStressEngine.MetricsUpdated -= OnCpuStressMetricsUpdated;
        _gpuStressEngine.MetricsUpdated -= OnGpuStressMetricsUpdated;
        _memoryStressEngine.MetricsUpdated -= OnMemoryStressMetricsUpdated;
        _storageStressEngine.MetricsUpdated -= OnStorageStressMetricsUpdated;
        _vramTest.MetricsUpdated -= OnVramTestMetricsUpdated;
        _cancellation.Cancel();
        _timer.Dispose();
        _cpuStressCancellation?.Dispose();
        _gpuStressCancellation?.Dispose();
        _memoryStressCancellation?.Dispose();
        _storageStressCancellation?.Dispose();
        _vramTestCancellation?.Dispose();
        _gpuStressEngine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _vramTest.Dispose();
        _cancellation.Dispose();
    }
}
