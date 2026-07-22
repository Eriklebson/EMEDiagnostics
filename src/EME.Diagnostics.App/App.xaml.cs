using EME.Diagnostics.Core.Services;
using EME.Diagnostics.Hardware;
using EME.Diagnostics.Reporting;
using EME.Diagnostics.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

namespace EME.Diagnostics.App;

public partial class App : Application
{
    private readonly IHost _host;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IHardwareMonitor, LibreHardwareMonitorService>();
                services.AddSingleton<ICpuStressEngine, CpuStressEngine>();
                services.AddSingleton<IGpuStressEngine, UnavailableGpuStressEngine>();
                services.AddSingleton<IReportService, PendingReportService>();
                services.AddSingleton<StressCatalogService>();
                services.AddSingleton<ViewModels.MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _host.StartAsync();
        _window = _host.Services.GetRequiredService<MainWindow>();
        _window.Closed += async (_, _) => { await _host.StopAsync(); _host.Dispose(); };
        _window.Activate();
    }
}
