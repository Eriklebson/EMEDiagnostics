using System.Threading;
using EME.Diagnostics.Core.Services;
using EME.Diagnostics.Hardware;
using EME.Diagnostics.Networking;
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

    public static IServiceProvider Services => ((App)Current)._host.Services;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, exceptionArgs) =>
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EMEDiagnostics");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "ui_crash.log"), $"{DateTime.Now:O}\r\n{exceptionArgs.Exception}\r\n{exceptionArgs.Message}");
            }
            catch { }
        };
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IHardwareMonitor, LibreHardwareMonitorService>();
                services.AddSingleton<ICpuStressEngine, CpuStressEngine>();
                services.AddSingleton<IGpuStressEngine, DirectX11GpuStressEngine>();
                services.AddSingleton<IMemoryStressEngine, MemoryStressEngine>();
                services.AddSingleton<IStorageStressEngine, StorageStressEngine>();
                services.AddSingleton<IReportRepository, ReportRepository>();
                services.AddSingleton<StressDataCollector>();
                services.AddSingleton<IReportService, ReportService>();
                services.AddSingleton<StressCatalogService>();
                services.AddSingleton<ServerService>();
                services.AddSingleton<ClientService>();
                services.AddSingleton<ViewModels.MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            ThreadPool.SetMinThreads(32, 32);
            await _host.StartAsync();
            _window = _host.Services.GetRequiredService<MainWindow>();
            _window.Closed += async (_, _) => { await _host.StopAsync(); _host.Dispose(); };
            _window.Activate();
        }
        catch (Exception ex)
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EMEDiagnostics");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "ui_crash.log"), $"{DateTime.Now:O}\r\n{ex}");
            throw;
        }
    }
}
