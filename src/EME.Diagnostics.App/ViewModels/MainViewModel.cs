using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;

namespace EME.Diagnostics.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IHardwareMonitor _hardware;
    private readonly ICpuStressEngine _cpuStressEngine;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(1));
    private readonly CancellationTokenSource _cancellation = new();
    private CancellationTokenSource? _cpuStressCancellation;

    private string _currentPage = "Dashboard";
    private HardwareSnapshot _snapshot = HardwareSnapshot.Empty;
    private string _status = "Inicializando sensores...";
    private CpuStressMetrics? _cpuStressMetrics;
    private StressStatus _cpuStressStatus = StressStatus.NotStarted;

    public string CurrentPage { get => _currentPage; private set => SetProperty(ref _currentPage, value); }
    public HardwareSnapshot Snapshot { get => _snapshot; private set => SetProperty(ref _snapshot, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public CpuStressMetrics? CpuStressMetrics { get => _cpuStressMetrics; private set => SetProperty(ref _cpuStressMetrics, value); }
    public StressStatus CpuStressStatus { get => _cpuStressStatus; private set => SetProperty(ref _cpuStressStatus, value); }

    public MainViewModel(IHardwareMonitor hardware, ICpuStressEngine cpuStressEngine)
    {
        _hardware = hardware;
        _cpuStressEngine = cpuStressEngine;
        _cpuStressEngine.MetricsUpdated += OnCpuStressMetricsUpdated;
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
            Snapshot = await _hardware.CaptureAsync(_cancellation.Token);
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

    private void OnCpuStressMetricsUpdated(object? sender, CpuStressMetrics metrics) => CpuStressMetrics = metrics;

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken)) await RefreshAsync();
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _cpuStressCancellation?.Cancel();
        _cpuStressEngine.MetricsUpdated -= OnCpuStressMetricsUpdated;
        _cancellation.Cancel();
        _timer.Dispose();
        _cpuStressCancellation?.Dispose();
        _cancellation.Dispose();
    }
}
