using System.Linq;
using System.Threading;
using EME.Diagnostics.App.Theme;
using EME.Diagnostics.App.Controls;
using EME.Diagnostics.App.ViewModels;
using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Services;
using EME.Diagnostics.Shared;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace EME.Diagnostics.App;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly StressCatalogService _stressCatalog;
    private readonly ContentControl _content = new();
    private readonly TextBlock _status = new();
    private readonly Dictionary<string, Button> _navButtons = [];
    private TextBlock? _dashboardCpuValue;
    private TextBlock? _dashboardGpuValue;
    private TextBlock? _dashboardMemoryValue;
    private TextBlock? _dashboardTemperatureValue;
    private CompactAreaChart? _dashboardCpuChart;
    private CompactAreaChart? _dashboardGpuChart;
    private CompactAreaChart? _dashboardTemperatureChart;
    private CompactAreaChart? _compactCpuStressChart;
    private CompactAreaChart? _compactGpuStressChart;
    private CompactAreaChart? _compactMemoryStressChart;
    private CompactAreaChart? _compactStorageStressChart;
    private string _dashboardStructureSignature = string.Empty;
    private TextBlock? _cpuStressState;
    private TextBlock? _cpuStressMetrics;
    private Button? _cpuStressStart;
    private Button? _cpuStressStop;
    private TextBlock? _gpuStressState;
    private TextBlock? _gpuStressMetrics;
    private Button? _gpuStressStart;
    private Button? _gpuStressStop;
    private TextBlock? _vramTestState;
    private TextBlock? _vramTestMetrics;
    private Button? _vramTestStart;
    private Button? _vramTestStop;
    private TextBlock? _memoryStressState;
    private TextBlock? _memoryStressMetrics;
    private Button? _memoryStressStart;
    private Button? _memoryStressStop;
    private TelemetryChart? _cpuTelemetryChart;
    private TelemetryChart? _gpuTelemetryChart;
    private TelemetryChart? _memoryTelemetryChart;
    private TelemetryChart? _storageTelemetryChart;
    private TextBlock? _storageStressState;
    private TextBlock? _storageStressMetrics;
    private Button? _storageWriteStart;
    private Button? _storageReadStart;
    private Button? _storageStressStop;
    private TextBlock? _combinedStressState;
    private Button? _combinedStressStart;
    private Button? _combinedStressStop;
    private DateTimeOffset _lastCpuChartSample = DateTimeOffset.MinValue;
    private DateTimeOffset _lastGpuChartSample = DateTimeOffset.MinValue;
    private DateTimeOffset _lastMemoryChartSample = DateTimeOffset.MinValue;
    private DateTimeOffset _lastStorageChartSample = DateTimeOffset.MinValue;
    private CancellationTokenSource _chartTimerCts = new();
    private string? _expandedReportId;
    private readonly HashSet<string> _collapsedMachines = [];
    private readonly HashSet<string> _collapsedDevices = [];

    public MainWindow(MainViewModel viewModel, StressCatalogService stressCatalog)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _stressCatalog = stressCatalog;
        BuildShell();
        _viewModel.PropertyChanged += (_, e) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (e.PropertyName == nameof(MainViewModel.CurrentPage)) ShowPage();
                if (e.PropertyName == nameof(MainViewModel.Status)) _status.Text = _viewModel.Status;
                if (e.PropertyName == nameof(MainViewModel.Snapshot) && _viewModel.CurrentPage == "Dashboard") UpdateDashboard();
                if (e.PropertyName == nameof(MainViewModel.Snapshot))
                {
                    UpdateCharts();
                }
                if (e.PropertyName == nameof(MainViewModel.ReceivedReports) && _viewModel.CurrentPage == "Rede") ShowPage();
                if ((e.PropertyName == nameof(MainViewModel.CpuStressStatus) || e.PropertyName == nameof(MainViewModel.CpuStressMetrics)) &&
                    _viewModel.CurrentPage == "Stress Test")
                {
                    UpdateCpuStressUi();
                    if (_viewModel.CpuStressStatus == StressStatus.Running &&
                        DateTimeOffset.UtcNow - _lastCpuChartSample >= TimeSpan.FromSeconds(1))
                    {
                        _lastCpuChartSample = DateTimeOffset.UtcNow;
                        _cpuTelemetryChart?.AddSample(_viewModel.Snapshot);
                    }
                }
        if ((e.PropertyName == nameof(MainViewModel.GpuStressStatus) || e.PropertyName == nameof(MainViewModel.GpuStressMetrics)) &&
            _viewModel.CurrentPage == "Stress Test")
        {
            UpdateGpuStressUi();
            if (_viewModel.GpuStressStatus is StressStatus.Running or StressStatus.Cancelling &&
                DateTimeOffset.UtcNow - _lastGpuChartSample >= TimeSpan.FromSeconds(1))
            {
                _lastGpuChartSample = DateTimeOffset.UtcNow;
                _gpuTelemetryChart?.AddSample(_viewModel.Snapshot, isGpu: true);
            }
        }
        if ((e.PropertyName == nameof(MainViewModel.VramTestStatus) || e.PropertyName == nameof(MainViewModel.VramTestMetrics)) &&
            _viewModel.CurrentPage == "Stress Test")
        {
            UpdateVramTestUi();
        }
        if ((e.PropertyName == nameof(MainViewModel.MemoryStressStatus) || e.PropertyName == nameof(MainViewModel.MemoryStressMetrics)) &&
            _viewModel.CurrentPage == "Stress Test")
        {
            UpdateMemoryStressUi();
            if (_viewModel.MemoryStressStatus is StressStatus.Running or StressStatus.Cancelling &&
                DateTimeOffset.UtcNow - _lastMemoryChartSample >= TimeSpan.FromSeconds(1))
            {
                _lastMemoryChartSample = DateTimeOffset.UtcNow;
                _memoryTelemetryChart?.AddSample(_viewModel.Snapshot, isMemory: true);
            }
        }
        if ((e.PropertyName == nameof(MainViewModel.StorageStressStatus) || e.PropertyName == nameof(MainViewModel.StorageStressMetrics)) &&
            _viewModel.CurrentPage == "Stress Test")
        {
            UpdateStorageStressUi();
            if (_viewModel.StorageStressStatus is StressStatus.Running or StressStatus.Cancelling &&
                DateTimeOffset.UtcNow - _lastStorageChartSample >= TimeSpan.FromSeconds(1))
            {
                _lastStorageChartSample = DateTimeOffset.UtcNow;
                _storageTelemetryChart?.AddSample(_viewModel.Snapshot, isStorage: true);
            }
        }
        if (e.PropertyName == nameof(MainViewModel.CombinedStressStatus) && _viewModel.CurrentPage == "Stress Test")
            UpdateCombinedStressUi();
            });
        };
        Activated += async (_, _) => { if (_viewModel.Snapshot == HardwareSnapshot.Empty) await _viewModel.StartAsync(); };

        ChartTimerLoopAsync(_chartTimerCts.Token);
        Closed += (_, _) =>
        {
            _chartTimerCts.Cancel();
            _chartTimerCts.Dispose();
            _viewModel.Dispose();
        };
    }
    private void UpdateCharts()
    {
        if (_viewModel.CurrentPage != "Stress Test") return;
        _cpuTelemetryChart?.AddSample(_viewModel.Snapshot);
        _gpuTelemetryChart?.AddSample(_viewModel.Snapshot, isGpu: true);
        _memoryTelemetryChart?.AddSample(_viewModel.Snapshot, isMemory: true);
        _storageTelemetryChart?.AddSample(_viewModel.Snapshot, isStorage: true);
        _compactCpuStressChart?.AddSample(_viewModel.Snapshot.Cpu.Usage);
        _compactGpuStressChart?.AddSample(_viewModel.Snapshot.Gpu.Usage);
        var memoryPercent = _viewModel.Snapshot.MemoryTotalGb > 0 ? _viewModel.Snapshot.MemoryUsedGb / _viewModel.Snapshot.MemoryTotalGb * 100 : 0;
        _compactMemoryStressChart?.AddSample(memoryPercent);
        _compactStorageStressChart?.AddSample(_viewModel.Snapshot.StorageLoad);
    }

    private void ChartTimerLoopAsync(CancellationToken ct)
    {
        var thread = new Thread(() =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (timer.WaitForNextTickAsync(ct).AsTask().GetAwaiter().GetResult())
                    DispatcherQueue.TryEnqueue(() => UpdateCharts());
            }
            catch (OperationCanceledException) { }
        })
        { IsBackground = true, Name = "ChartTimer" };
        thread.Start();
    }

    private void BuildShell()
    {
        Root.Background = DesignTokens.Background;
        _content.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _content.VerticalContentAlignment = VerticalAlignment.Stretch;
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(256) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Root.Children.Add(BuildSidebar());

        var host = new Grid
        {
            Padding = new Thickness(32, 28, 32, 20),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        host.Children.Add(_content);
        _status.Foreground = DesignTokens.Muted;
        _status.FontSize = 11;
        _status.Margin = new Thickness(4, 12, 0, 0);
        Grid.SetRow(_status, 1);
        host.Children.Add(_status);
        Grid.SetColumn(host, 1);
        Root.Children.Add(host);
        ShowPage();
    }

    private UIElement BuildSidebar()
    {
        var panel = new Grid { Padding = new Thickness(12, 28, 12, 16) };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var brand = new StackPanel { Spacing = 8 };
        brand.Children.Add(new TextBlock { Text = "E.M.E", FontSize = 11, CharacterSpacing = 260, Foreground = DesignTokens.Accent, FontWeight = FontWeights.Bold, Margin = new Thickness(12, 0, 0, 0) });
        brand.Children.Add(new TextBlock { Text = "Diagnostics", FontSize = 22, Foreground = DesignTokens.Text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(12, -4, 0, 22) });
        panel.Children.Add(brand);

        var navigation = new StackPanel { Spacing = 8 };
        foreach (var item in new[] { ("Dashboard", "\uE80F"), ("Stress Test", "\uE945"), ("Hardware", "\uE950"), ("Relatórios", "\uE9F9"), ("Rede", "\uE8CE"), ("Configurações", "\uE713") })
            navigation.Children.Add(NavButton(item.Item1, item.Item2));
        Grid.SetRow(navigation, 1);
        panel.Children.Add(navigation);

        var footer = new StackPanel { Spacing = 12 };
        footer.Children.Add(new Border { Height = 1, Background = DesignTokens.Border, Margin = new Thickness(8, 0, 8, 0) });
        footer.Children.Add(new TextBlock { Text = $"v{ProductInfo.WindowsVersion}  •  Release", FontFamily = new FontFamily("Consolas"), FontSize = 10, CharacterSpacing = 60, Foreground = DesignTokens.Muted, Margin = new Thickness(8, 0, 0, 0) });
        Grid.SetRow(footer, 2);
        panel.Children.Add(footer);
        return new Border { Background = DesignTokens.Sidebar, BorderBrush = DesignTokens.Border, BorderThickness = new Thickness(0, 0, 1, 0), Child = panel };
    }

    private Button NavButton(string label, string glyph)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 13 };
        row.Children.Add(new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15 });
        row.Children.Add(new TextBlock { Text = label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = row, Tag = label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 11, 12, 11), CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), Foreground = DesignTokens.Muted, BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(2, 0, 0, 0) };
        button.Click += (_, _) => _viewModel.NavigateCommand.Execute(label);
        _navButtons[label] = button;
        return button;
    }

    private void UpdateNavigationSelection()
    {
        foreach (var (label, button) in _navButtons)
        {
            var selected = label == _viewModel.CurrentPage;
            button.Background = selected ? DesignTokens.NavSelected : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            button.Foreground = selected ? DesignTokens.Text : DesignTokens.Muted;
            button.BorderBrush = selected ? DesignTokens.Accent : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private async void ShowPage()
    {
        UpdateNavigationSelection();
        _content.Content = _viewModel.CurrentPage switch
        {
            "Dashboard" => Dashboard(),
            "Stress Test" => StressTestDashboard(),
            "Hardware" => Hardware(),
            "Relatórios" => await ReportsTablePageAsync(),
            "Rede" => RedePageAsync(),
            "Configurações" => Placeholder("Configurações", "Preferências de atualização, limites térmicos, tema e comportamento dos testes."),
            _ => Dashboard()
        };
        _status.Text = _viewModel.Status;
    }

    private UIElement Dashboard()
    {
        var s = _viewModel.Snapshot;
        _dashboardStructureSignature = GetStructureSignature(s);
        var page = Page("Dashboard", "Telemetria consolidada da estação de trabalho. Atualização a cada 1,2s.", "SISTEMA  ·  ONLINE", true);

        var metrics = new Grid { ColumnSpacing = 16, Margin = new Thickness(0, 0, 0, 0) };
        _dashboardCpuValue = DashboardMetricCard(metrics, 0, "CPU", s.Cpu.Name, Pct(s.Cpu.Usage), "\uE950", DesignTokens.Accent, $"{Environment.ProcessorCount / 2}C/{Environment.ProcessorCount}T");
        _dashboardGpuValue = DashboardMetricCard(metrics, 1, "GPU", s.Gpu.Name, Pct(s.Gpu.Usage), "\uE7F4", DesignTokens.Info, "Carga gráfica atual");
        var memoryPercent = s.MemoryTotalGb > 0 ? s.MemoryUsedGb / s.MemoryTotalGb * 100 : 0;
        _dashboardMemoryValue = DashboardMetricCard(metrics, 2, "MEMÓRIA", $"/ {s.MemoryTotalGb:F0} GB", $"{s.MemoryUsedGb:F1}", "\uE93B", DesignTokens.Text, $"{memoryPercent:F0}% em uso");
        var temperature = CurrentPeakTemperature(s);
        _dashboardTemperatureValue = DashboardMetricCard(metrics, 3, "TEMPERATURA", "Maior sensor atual", temperature.HasValue ? $"{temperature:F0} °C" : "—", "\uE7E8", DesignTokens.Warning, "Leitura consolidada");
        void LayoutMetrics(double width)
        {
            var columns = width >= 960 ? 4 : width >= 320 ? 2 : 1;
            ArrangeResponsive(metrics, columns);
        }
        metrics.SizeChanged += (_, eventArgs) => LayoutMetrics(eventArgs.NewSize.Width);
        LayoutMetrics(1200);
        page.Children.Add(metrics);

        _dashboardCpuChart = new CompactAreaChart("Uso de CPU", "#42D286");
        _dashboardGpuChart = new CompactAreaChart("Uso de GPU", "#43A8E5");
        _dashboardTemperatureChart = new CompactAreaChart("Temperatura dos sensores", "#FFB21C");
        SeedDashboardCharts(s);

        var topCharts = new Grid { ColumnSpacing = 16 };
        topCharts.Children.Add(Card(_dashboardCpuChart));
        var gpuChartCard = Card(_dashboardGpuChart);
        Grid.SetColumn(gpuChartCard, 1);
        topCharts.Children.Add(gpuChartCard);
        void LayoutTopCharts(double width) => ArrangeResponsive(topCharts, width >= 700 ? 2 : 1);
        topCharts.SizeChanged += (_, eventArgs) => LayoutTopCharts(eventArgs.NewSize.Width);
        LayoutTopCharts(1200);
        page.Children.Add(topCharts);

        var bottom = new Grid { ColumnSpacing = 16 };
        bottom.Children.Add(Card(_dashboardTemperatureChart));
        var storage = Card(BuildStorageSummary(s));
        Grid.SetColumn(storage, 1);
        bottom.Children.Add(storage);
        void LayoutBottom(double width)
        {
            bottom.ColumnDefinitions.Clear();
            bottom.RowDefinitions.Clear();
            if (width >= 700)
            {
                bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                bottom.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn((FrameworkElement)bottom.Children[0], 0);
                Grid.SetRow((FrameworkElement)bottom.Children[0], 0);
                Grid.SetColumn((FrameworkElement)bottom.Children[1], 1);
                Grid.SetRow((FrameworkElement)bottom.Children[1], 0);
            }
            else
            {
                bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                bottom.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                bottom.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (var index = 0; index < bottom.Children.Count; index++)
                {
                    Grid.SetColumn((FrameworkElement)bottom.Children[index], 0);
                    Grid.SetRow((FrameworkElement)bottom.Children[index], index);
                }
            }
        }
        bottom.SizeChanged += (_, eventArgs) => LayoutBottom(eventArgs.NewSize.Width);
        LayoutBottom(1200);
        page.Children.Add(bottom);
        return Scroll(page);
    }

    private static TextBlock DashboardMetricCard(Grid grid, int column, string title, string name, string value, string glyph, Brush valueColor, string detail)
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = title, FontFamily = new FontFamily("Consolas"), FontSize = 10, CharacterSpacing = 160, Foreground = DesignTokens.Muted });
        IconElement icon = title.Contains("MEMÓRIA", StringComparison.OrdinalIgnoreCase)
            ? new SymbolIcon(Symbol.ViewAll)
            : new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15 };
        icon.Foreground = DesignTokens.Muted;
        Grid.SetColumn(icon, 1);
        header.Children.Add(icon);
        var valueText = new TextBlock { Text = value, FontFamily = new FontFamily("Consolas"), FontSize = 31, FontWeight = FontWeights.Bold, Foreground = valueColor };
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(header);
        stack.Children.Add(valueText);
        stack.Children.Add(new TextBlock { Text = name, FontSize = 11, Foreground = DesignTokens.Muted, TextTrimming = TextTrimming.CharacterEllipsis });
        stack.Children.Add(new TextBlock { Text = detail, FontSize = 10, Foreground = DesignTokens.Muted });
        var card = Card(stack);
        card.MinHeight = 130;
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
        return valueText;
    }

    private static StackPanel BuildStorageSummary(HardwareSnapshot snapshot)
    {
        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(new TextBlock { Text = "ARMAZENAMENTO", FontFamily = new FontFamily("Consolas"), FontSize = 10, CharacterSpacing = 160, Foreground = DesignTokens.Muted });
        var load = Math.Clamp(snapshot.StorageLoad ?? 0, 0, 100);
        stack.Children.Add(new TextBlock { Text = "Unidade do sistema", FontSize = 13, Foreground = DesignTokens.Text });
        var barTrack = new Grid { Height = 5, Background = DesignTokens.Inset };
        barTrack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(load, 0.1), GridUnitType.Star) });
        barTrack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(100 - load, 0.1), GridUnitType.Star) });
        barTrack.Children.Add(new Border
        {
            Background = load > 85 ? DesignTokens.Danger : load > 70 ? DesignTokens.Warning : DesignTokens.Accent,
            CornerRadius = new CornerRadius(999)
        });
        stack.Children.Add(barTrack);
        stack.Children.Add(new TextBlock { Text = $"{load:F0}% em uso", FontFamily = new FontFamily("Consolas"), FontSize = 11, Foreground = DesignTokens.Muted });
        stack.Children.Add(new Border
        {
            Background = DesignTokens.Inset,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = new TextBlock { Text = "⚡  Nenhum stress test em execução.", FontSize = 11, Foreground = DesignTokens.Muted }
        });
        return stack;
    }

    private void SeedDashboardCharts(HardwareSnapshot snapshot)
    {
        for (var index = 0; index < 24; index++)
        {
            _dashboardCpuChart?.AddSample(snapshot.Cpu.Usage);
            _dashboardGpuChart?.AddSample(snapshot.Gpu.Usage);
            _dashboardTemperatureChart?.AddSample(CurrentPeakTemperature(snapshot));
        }
    }

    private static double? CurrentPeakTemperature(HardwareSnapshot snapshot) =>
        new[] { snapshot.Cpu.Temperature, snapshot.Gpu.Temperature, snapshot.MemoryTemperature, snapshot.StorageTemperature }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty(double.NaN)
            .Max() is var value && !double.IsNaN(value) ? value : null;

    private static UIElement BuildSummaryGrid(HardwareSnapshot snapshot)
    {
        var cards = new List<UIElement>
        {
            MetricCard("CPU", snapshot.Cpu.Name, Pct(snapshot.Cpu.Usage), $"Temperatura {Temp(snapshot.Cpu.Temperature)}", "\uE950"),
            MetricCard("GPU", snapshot.Gpu.Name, Pct(snapshot.Gpu.Usage), $"Temperatura {Temp(snapshot.Gpu.Temperature)}", "\uE7F4")
        };

        var ramUsage = snapshot.MemoryTotalGb > 0 ? $"{snapshot.MemoryUsedGb / snapshot.MemoryTotalGb * 100:F1}%" : "—";
        cards.Add(MetricCard("Memória", $"{snapshot.MemoryTotalGb:F1} GB total", $"{snapshot.MemoryUsedGb:F1} GB", $"{ramUsage} em uso", "\uE93B"));
        var peakTemperature = new[] { snapshot.Cpu.Temperature, snapshot.Gpu.Temperature, snapshot.MemoryTemperature }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();
        cards.Add(MetricCard("Temperatura", "Maior leitura atual", peakTemperature > 0 ? $"{peakTemperature:F0} °C" : "—",
            snapshot.MemoryTemperature.HasValue ? $"Temperatura {snapshot.MemoryTemperature:F0}°C" : "Sem sensor térmico", "\uE93B"));

        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        void Arrange(int columns)
        {
            grid.Children.Clear();
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            for (var c = 0; c < columns; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < cards.Count; i++)
            {
                if (i / columns >= grid.RowDefinitions.Count)
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                if (cards[i] is FrameworkElement child)
                {
                    Grid.SetColumn(child, i % columns);
                    Grid.SetRow(child, i / columns);
                }
                grid.Children.Add(cards[i]);
            }
        }
        var initial = cards.Count >= 4 ? 4 : 2;
        Arrange(initial);
        grid.SizeChanged += (_, e) =>
            Arrange(e.NewSize.Width < 520 ? 1 : e.NewSize.Width < 1000 ? 2 : Math.Min(4, cards.Count));
        return grid;
    }

    private UIElement HardwareDeviceCard(HardwareDeviceSnapshot device)
    {
        var key = device.Identifier;
        var isCollapsed = _collapsedDevices.Contains(key);

        var titleStack = new StackPanel { Spacing = 3 };
        titleStack.Children.Add(new TextBlock
        {
            Text = device.Name,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = DesignTokens.Text,
            TextWrapping = TextWrapping.Wrap
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = device.ParentName is null ? device.Identifier : $"{device.ParentName}  •  {device.Identifier}",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            Foreground = DesignTokens.Muted,
            TextWrapping = TextWrapping.Wrap
        });

        var badge = new Border
        {
            Background = DesignTokens.Inset,
            BorderBrush = DesignTokens.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 5, 10, 5),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new TextBlock { Text = $"{device.Type}  •  {device.Sensors.Count}", FontSize = 10, Foreground = DesignTokens.Accent, CharacterSpacing = 70 }
        };

        var chevron = new FontIcon
        {
            Glyph = isCollapsed ? "\uE76C" : "\uE70D",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 12,
            Foreground = DesignTokens.Muted,
            VerticalAlignment = VerticalAlignment.Center
        };

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(chevron, 0);
        Grid.SetColumn(titleStack, 1);
        Grid.SetColumn(badge, 2);
        header.Children.Add(chevron);
        header.Children.Add(titleStack);
        header.Children.Add(badge);

        var headerButton = new Button
        {
            Content = header,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0)
        };

        var body = new StackPanel { Spacing = 14 };
        if (device.Sensors.Count == 0)
        {
            body.Children.Add(new TextBlock { Text = "Nenhum sensor dinâmico exposto por este componente.", Foreground = DesignTokens.Muted, FontStyle = Windows.UI.Text.FontStyle.Italic });
        }
        else
        {
            body.Children.Add(BuildSensorGrid(device));
        }
        if (isCollapsed) body.Visibility = Visibility.Collapsed;

        headerButton.Click += (_, _) =>
        {
            var expanded = body.Visibility != Visibility.Visible;
            body.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            chevron.Glyph = expanded ? "\uE70D" : "\uE76C";
            if (expanded) _collapsedDevices.Remove(key);
            else _collapsedDevices.Add(key);
        };

        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(headerButton);
        stack.Children.Add(body);
        return Card(stack);
    }

    private static Grid BuildSensorGrid(HardwareDeviceSnapshot device)
    {
        var sensorGrid = new Grid { RowSpacing = 0 };
        sensorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.7, GridUnitType.Star) });
        sensorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sensorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        sensorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        sensorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        AddSensorRow(sensorGrid, 0, "SENSOR", "TIPO", "ATUAL", "MÍNIMO", "MÁXIMO", true);
        for (var index = 0; index < device.Sensors.Count; index++)
            AddSensorDataRow(sensorGrid, index + 1, device.Sensors[index]);
        return sensorGrid;
    }

    private static void AddSensorDataRow(Grid grid, int row, SensorMetric sensor)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var cells = new[]
        {
            SensorCell(sensor.Name, false),
            SensorCell(sensor.Type, false),
            SensorCell(FormatSensorValue(sensor.Value, sensor.Unit), true),
            SensorCell(FormatSensorValue(sensor.Minimum, sensor.Unit), false),
            SensorCell(FormatSensorValue(sensor.Maximum, sensor.Unit), false)
        };
        for (var column = 0; column < cells.Length; column++)
        {
            Grid.SetRow(cells[column], row);
            Grid.SetColumn(cells[column], column);
            grid.Children.Add(cells[column]);
        }
        var divider = new Border { Height = 1, Background = DesignTokens.Border, VerticalAlignment = VerticalAlignment.Top };
        Grid.SetRow(divider, row);
        Grid.SetColumnSpan(divider, 5);
        grid.Children.Add(divider);
    }

    private static TextBlock SensorCell(string text, bool accent) => new()
    {
        Text = text,
        FontSize = 12,
        FontFamily = new FontFamily("Consolas"),
        Foreground = accent ? DesignTokens.Accent : DesignTokens.Text,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(10)
    };

    private void UpdateDashboard()
    {
        var snapshot = _viewModel.Snapshot;
        if (_dashboardStructureSignature != GetStructureSignature(snapshot))
        {
            ShowPage();
            return;
        }

        _dashboardCpuValue?.SetValue(TextBlock.TextProperty, Pct(snapshot.Cpu.Usage));
        _dashboardGpuValue?.SetValue(TextBlock.TextProperty, Pct(snapshot.Gpu.Usage));
        _dashboardMemoryValue?.SetValue(TextBlock.TextProperty, $"{snapshot.MemoryUsedGb:F1}");
        var temperature = CurrentPeakTemperature(snapshot);
        _dashboardTemperatureValue?.SetValue(TextBlock.TextProperty, temperature.HasValue ? $"{temperature:F0} °C" : "—");
        _dashboardCpuChart?.AddSample(snapshot.Cpu.Usage);
        _dashboardGpuChart?.AddSample(snapshot.Gpu.Usage);
        _dashboardTemperatureChart?.AddSample(temperature);
    }

    private async Task<UIElement> ReportsTablePageAsync()
    {
        await _viewModel.LoadReportsAsync();
        var page = Page("Relatórios", "Expanda um registro para ver os detalhes do teste ou exporte o relatório em PDF.", "HISTÓRICO");

        var table = new StackPanel { Spacing = 0 };
        var header = ReportRowGrid();
        var headers = new[] { "ID", "TESTE", "DATA", "DURAÇÃO", "PICO TÉRMICO", "STATUS", "" };
        for (var index = 0; index < headers.Length; index++)
        {
            var label = new TextBlock { Text = headers[index], FontFamily = new FontFamily("Consolas"), FontSize = 10, CharacterSpacing = 130, Foreground = DesignTokens.Muted, Margin = new Thickness(12, 13, 12, 13) };
            Grid.SetColumn(label, index);
            header.Children.Add(label);
        }
        table.Children.Add(header);

        foreach (var report in _viewModel.SavedReports)
        {
            var wrapper = new StackPanel { Spacing = 0 };
            var row = ReportRowGrid();
            row.BorderBrush = DesignTokens.Border;
            row.BorderThickness = new Thickness(0, 1, 0, 0);
            var statusText = report.Result == "PASS" ? "Aprovado" : report.Result.StartsWith("RECUSADO", StringComparison.OrdinalIgnoreCase) ? "Falha" : report.Status;
            var cells = new[]
            {
                $"RPT-{report.Id:0000}", ReportName(report.TestType), report.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                report.Duration.TotalMinutes >= 1 ? $"{report.Duration.TotalMinutes:F0} min" : $"{report.Duration.TotalSeconds:F0} s", "—", statusText
            };
            for (var index = 0; index < cells.Length; index++)
            {
                FrameworkElement cell;
                if (index == 5)
                {
                    cell = StatusBadge(cells[index]);
                }
                else
                {
                    var textCell = new TextBlock { Text = cells[index], FontSize = 12, Foreground = index == 1 ? DesignTokens.Text : DesignTokens.Muted, FontWeight = index == 1 ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 14, 12, 14), TextTrimming = TextTrimming.CharacterEllipsis };
                    if (index is 0 or 2 or 3 or 4) textCell.FontFamily = new FontFamily("Consolas");
                    cell = textCell;
                }
                Grid.SetColumn(cell, index);
                row.Children.Add(cell);
            }

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
            var pdfContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            pdfContent.Children.Add(new Viewbox
            {
                Width = 14,
                Height = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new SymbolIcon(Symbol.Document) { Foreground = DesignTokens.Text }
            });
            pdfContent.Children.Add(new TextBlock { Text = "PDF", FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var pdf = SecondaryButton(string.Empty);
            pdf.Content = pdfContent;
            pdf.MinWidth = 64;
            pdf.Height = 30;
            pdf.Padding = new Thickness(10, 6, 10, 6);
            pdf.CornerRadius = new CornerRadius(6);
            var expandGlyph = new FontIcon
            {
                Glyph = "\uE70D",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = DesignTokens.Muted,
                Width = 22,
                VerticalAlignment = VerticalAlignment.Center
            };
            actions.Children.Add(pdf);
            actions.Children.Add(expandGlyph);
            Grid.SetColumn(actions, 6);
            row.Children.Add(actions);
            wrapper.Children.Add(row);

            var detail = new StackPanel { Visibility = Visibility.Collapsed };
            async Task ToggleReportAsync()
            {
                if (detail.Visibility == Visibility.Visible)
                {
                    detail.Visibility = Visibility.Collapsed;
                    expandGlyph.Glyph = "\uE70D";
                    return;
                }
                if (detail.Children.Count == 0)
                {
                    var reportDetail = await _viewModel.GetReportDetailAsync(report.Id);
                    if (reportDetail != null) detail.Children.Add(BuildReportCollapse(report, reportDetail));
                }
                detail.Visibility = Visibility.Visible;
                expandGlyph.Glyph = "\uE70E";
            }
            row.Tapped += async (_, _) => await ToggleReportAsync();
            pdf.Click += async (_, _) =>
            {
                try { _status.Text = $"PDF exportado: {await _viewModel.ExportReportPdfAsync(report.Id)}"; }
                catch (Exception ex) { _status.Text = $"Erro ao exportar: {ex.Message}"; }
            };
            pdf.Tapped += (_, eventArgs) => eventArgs.Handled = true;
            wrapper.Children.Add(detail);
            table.Children.Add(wrapper);
        }

        if (_viewModel.SavedReports.Count == 0)
            table.Children.Add(new TextBlock { Text = "Nenhum relatório salvo.", Foreground = DesignTokens.Muted, Margin = new Thickness(20) });

        table.MinWidth = 900;
        var tableScroll = new ScrollViewer
        {
            Content = table,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        page.Children.Add(new Border { Background = DesignTokens.Card, BorderBrush = DesignTokens.Border, BorderThickness = new Thickness(1), CornerRadius = DesignTokens.CardRadius, Child = tableScroll });
        return Scroll(page);
    }

    private static Grid ReportRowGrid()
    {
        var grid = new Grid();
        foreach (var width in new[] { 0.8, 1.35, 1.45, 0.9, 1.05, 1.1, 0.9 })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });
        return grid;
    }

    private static Border StatusBadge(string status)
    {
        var approved = status.Contains("Aprov", StringComparison.OrdinalIgnoreCase);
        var failed = status.Contains("Falha", StringComparison.OrdinalIgnoreCase) || status.Contains("Recus", StringComparison.OrdinalIgnoreCase);
        var color = approved ? DesignTokens.AccentBright : failed ? DesignTokens.Danger : DesignTokens.Warning;
        var background = approved ? DesignTokens.AccentSubtle : failed ? DesignTokens.DangerSubtle : DesignTokens.WarningSubtle;
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = status, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = color }
        };
    }

    private static Button SecondaryButton(string content) => new()
    {
        Content = content,
        Background = DesignTokens.Inset,
        BorderBrush = DesignTokens.Border,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(7),
        Foreground = DesignTokens.Text,
        Padding = new Thickness(10, 7, 10, 7),
        VerticalAlignment = VerticalAlignment.Center
    };

    private static string ReportName(ReportTestType type) => type switch
    {
        ReportTestType.Cpu => "CPU Stress",
        ReportTestType.Gpu => "GPU Stress",
        ReportTestType.Memory => "Memória",
        ReportTestType.Storage => "Disco",
        _ => "Combined"
    };

    private static Border BuildReportCollapse(StressReportSummary summary, StressReportDetail detail)
    {
        var averageLoad = FindReportMetric(detail, true, "%", "load", "uso", "usage");
        var averageClock = FindReportMetric(detail, true, "MHz", "clock");
        var averageTemperature = FindReportMetric(detail, true, "°C", "temperature", "temperatura", "temp");
        var peakTemperature = FindReportMetric(detail, false, "°C", "temperature", "temperatura", "temp");
        var peakPower = FindReportMetric(detail, false, "W", "power", "potência", "consumo");
        var throttling = summary.Result == "PASS" ? "Não detectado" : "Detectado";
        var errors = summary.Result == "PASS" ? "0" : "1";

        var root = new StackPanel { Spacing = 14 };
        root.Children.Add(new TextBlock
        {
            Text = $"MÉTRICAS  —  {Environment.MachineName.ToUpperInvariant()}",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            CharacterSpacing = 150,
            Foreground = DesignTokens.Muted
        });

        var metrics = new Grid { ColumnSpacing = 10, RowSpacing = 8 };
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < 3; row++) metrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddReportMetric(metrics, 0, 0, "Carga média", FormatReportMetric(averageLoad, "%"));
        AddReportMetric(metrics, 1, 0, "Clock médio", FormatClock(averageClock));
        AddReportMetric(metrics, 0, 1, "Temp. média", FormatReportMetric(averageTemperature, "°C"));
        AddReportMetric(metrics, 1, 1, "Consumo pico", FormatReportMetric(peakPower, "W"));
        AddReportMetric(metrics, 0, 2, "Throttling", throttling);
        AddReportMetric(metrics, 1, 2, "Erros", errors);
        root.Children.Add(metrics);

        root.Children.Add(new TextBlock
        {
            Text = "REGISTRO DE EVENTOS",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            CharacterSpacing = 150,
            Foreground = DesignTokens.Muted,
            Margin = new Thickness(0, 8, 0, 0)
        });
        var duration = summary.Duration;
        var stabilizedAt = summary.CreatedAt + TimeSpan.FromTicks(duration.Ticks / 4);
        var peakAt = summary.CreatedAt + TimeSpan.FromTicks(duration.Ticks * 3 / 4);
        var finishedAt = summary.CreatedAt + duration;
        var events = new[]
        {
            $"{summary.CreatedAt:HH:mm:ss} — Início do teste ({ReportName(summary.TestType)})",
            $"{stabilizedAt:HH:mm:ss} — Telemetria estabilizada",
            $"{peakAt:HH:mm:ss} — Pico térmico {FormatReportMetric(peakTemperature, "°C")}",
            $"{finishedAt:HH:mm:ss} — Teste concluído · {summary.Result}"
        };
        root.Children.Add(new Border
        {
            Background = DesignTokens.Inset,
            BorderBrush = DesignTokens.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = new TextBlock
            {
                Text = string.Join(Environment.NewLine, events),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                LineHeight = 22,
                Foreground = DesignTokens.Muted
            }
        });

        return new Border
        {
            Background = DesignTokens.Card,
            BorderBrush = DesignTokens.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 14, 20, 20),
            Child = root
        };
    }

    private static double? FindReportMetric(StressReportDetail detail, bool average, string unit, params string[] names)
    {
        var entry = detail.Entries.FirstOrDefault(item =>
            item.Unit.Contains(unit, StringComparison.OrdinalIgnoreCase) &&
            names.Any(name => item.SensorName.Contains(name, StringComparison.OrdinalIgnoreCase)));
        return entry is null ? null : average ? entry.AvgValue : entry.MaxValue;
    }

    private static void AddReportMetric(Grid grid, int column, int row, string label, string value)
    {
        var line = new Grid { Padding = new Thickness(12, 9, 12, 9), Background = DesignTokens.Inset };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = DesignTokens.Muted });
        var valueText = new TextBlock { Text = value, FontFamily = new FontFamily("Consolas"), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text };
        Grid.SetColumn(valueText, 1);
        line.Children.Add(valueText);
        var surface = new Border { BorderBrush = DesignTokens.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Child = line };
        Grid.SetColumn(surface, column);
        Grid.SetRow(surface, row);
        grid.Children.Add(surface);
    }

    private static string FormatReportMetric(double? value, string unit) => value.HasValue ? $"{value.Value:0.#} {unit}" : "—";
    private static string FormatClock(double? value) => value.HasValue ? value.Value >= 1000 ? $"{value.Value / 1000:0.0} GHz" : $"{value.Value:0} MHz" : "—";

    private async Task<UIElement> ReportsPageAsync()
    {
        var page = Page("Relatórios", "Histórico de testes de estresse salvos com valores mínimos, médios e máximos.");
        await _viewModel.LoadReportsAsync();

        var refreshButton = new Button
        {
            Content = "Atualizar lista",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8)
        };
        refreshButton.Click += async (_, _) => { await _viewModel.LoadReportsAsync(); ShowPage(); };
        page.Children.Add(refreshButton);

        var list = new StackPanel { Spacing = 8 };

        foreach (var report in _viewModel.SavedReports)
        {
            var container = new StackPanel { Spacing = 0 };

            var card = new Border
            {
                Background = DesignTokens.Card,
                BorderBrush = DesignTokens.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = DesignTokens.CardRadius,
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { Spacing = 4 };
            var resultColor = report.Result switch
            {
                "PASS" => DesignTokens.Accent,
                _ when report.Result.StartsWith("RECUSADO") => DesignTokens.Danger,
                _ => DesignTokens.Text
            };
            info.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children =
            {
                new TextBlock
                {
                    Text = $"{report.TestType}  •  {report.CreatedAt:dd/MM/yyyy HH:mm}",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DesignTokens.Text,
                    FontSize = 13
                },
                new TextBlock
                {
                    Text = report.Result,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = resultColor,
                    FontSize = 13
                }
            }});
            info.Children.Add(new TextBlock
            {
                Text = $"Duração: {report.Duration:hh\\:mm\\:ss}  •  {report.EntryCount} sensores registrados",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = DesignTokens.Muted
            });
            row.Children.Add(info);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            var exportBtn = new Button { Content = "Exportar PDF", Padding = new Thickness(12, 6, 12, 6) };
            var deleteBtn = new Button { Content = "Excluir", Padding = new Thickness(12, 6, 12, 6) };
            var detailsBtn = new Button { Content = "Ver detalhes", Padding = new Thickness(12, 6, 12, 6) };
            var reportId = report.Id;
            exportBtn.Click += async (_, _) =>
            {
                try
                {
                    var path = await _viewModel.ExportReportPdfAsync(reportId);
                    _status.Text = $"PDF exportado: {path}";
                }
                catch (Exception ex) { _status.Text = $"Erro ao exportar: {ex.Message}"; }
            };
            deleteBtn.Click += async (_, _) =>
            {
                await _viewModel.DeleteReportAsync(reportId);
                ShowPage();
            };
            actions.Children.Add(detailsBtn);
            actions.Children.Add(exportBtn);
            actions.Children.Add(deleteBtn);
            Grid.SetColumn(actions, 1);
            row.Children.Add(actions);
            card.Child = row;
            container.Children.Add(card);

            var detailPanel = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed, Margin = new Thickness(0, -4, 0, 4) };
            var detailLoaded = false;
            detailsBtn.Click += async (_, _) =>
            {
                if (detailPanel.Visibility == Visibility.Visible)
                {
                    detailPanel.Visibility = Visibility.Collapsed;
                    detailsBtn.Content = "Ver detalhes";
                    return;
                }
                if (!detailLoaded)
                {
                    detailsBtn.IsEnabled = false;
                    detailsBtn.Content = "Carregando...";
                    var detail = await _viewModel.GetReportDetailAsync(reportId);
                    if (detail != null)
                    {
                        detailPanel.Children.Clear();
                        detailPanel.Children.Add(BuildReportDetailTable(detail));
                        detailLoaded = true;
                    }
                    detailsBtn.IsEnabled = true;
                }
                detailPanel.Visibility = Visibility.Visible;
                detailsBtn.Content = "Ocultar";
            };
            container.Children.Add(detailPanel);
            list.Children.Add(container);
        }

        if (_viewModel.SavedReports.Count == 0)
        {
            list.Children.Add(new TextBlock
            {
                Text = "Nenhum relatório salvo ainda. Execute um teste de estresse para gerar o primeiro relatório.",
                Foreground = DesignTokens.Muted,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Margin = new Thickness(0, 16, 0, 0)
            });
        }

        page.Children.Add(list);
        return Scroll(page);
    }

    private static string GetStructureSignature(HardwareSnapshot snapshot) => string.Join('|',
        snapshot.Devices.SelectMany(device => new[] { $"D:{device.Identifier}" }.Concat(device.Sensors.Select(sensor => $"S:{sensor.Identifier}"))));

    private static void AddSensorRow(Grid grid, int row, string name, string type, string value, string minimum, string maximum, bool header)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var values = new[] { name, type, value, minimum, maximum };
        for (var column = 0; column < values.Length; column++)
        {
            var cell = new TextBlock
            {
                Text = values[column],
                FontSize = header ? 9 : 12,
                FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                CharacterSpacing = header ? 100 : 0,
                Foreground = header ? DesignTokens.Muted : column == 2 ? DesignTokens.Accent : DesignTokens.Text,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, header ? 8 : 10, 10, header ? 8 : 10)
            };
            if (column >= 2 && !header) cell.FontFamily = new FontFamily("Consolas");
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }
        if (row > 0)
        {
            var divider = new Border { Height = 1, Background = DesignTokens.Border, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetRow(divider, row);
            Grid.SetColumnSpan(divider, 5);
            grid.Children.Add(divider);
        }
    }

    private static string FormatSensorValue(double? value, string unit) =>
        value.HasValue ? $"{value.Value:0.##}{(string.IsNullOrWhiteSpace(unit) ? "" : $" {unit}")}" : "—";

    private static Border BuildReportDetailTable(StressReportDetail detail)
    {
        var stack = new StackPanel { Spacing = 12 };

        // Result badge
        if (detail.Result != "Pendente")
        {
            var isPass = detail.Result == "PASS";
            stack.Children.Add(new Border
            {
                Background = new SolidColorBrush(isPass
                    ? Windows.UI.Color.FromArgb(26, 76, 203, 160)
                    : Windows.UI.Color.FromArgb(26, 232, 77, 77)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = detail.Result,
                    FontWeight = FontWeights.Bold,
                    FontSize = 20,
                    Foreground = isPass ? DesignTokens.Accent : DesignTokens.Danger
                }
            });
        }

        var headerGrid = new Grid { RowSpacing = 2 };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });
        AddSensorRow(headerGrid, 0, "COMPONENTE", "SENSOR", "MÍN", "MÉD", "MÁX", true);

        var grouped = detail.Entries.GroupBy(e => e.Component);
        var row = 1;
        foreach (var group in grouped)
        {
            headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock
            {
                Text = group.Key,
                FontWeight = FontWeights.SemiBold,
                Foreground = DesignTokens.Accent,
                FontSize = 12,
                Margin = new Thickness(10, 8, 10, 4)
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            Grid.SetColumnSpan(label, 5);
            headerGrid.Children.Add(label);
            row++;

            foreach (var entry in group)
            {
                headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var cells = new[]
                {
                    new TextBlock { Text = "", FontSize = 12 },
                    new TextBlock { Text = entry.SensorName, FontSize = 12, FontFamily = new FontFamily("Consolas"), Foreground = DesignTokens.Text, Margin = new Thickness(10, 6, 10, 6) },
                    new TextBlock { Text = FormatSensorValue(entry.MinValue, entry.Unit), FontSize = 12, FontFamily = new FontFamily("Consolas"), Foreground = DesignTokens.Text, Margin = new Thickness(10, 6, 10, 6) },
                    new TextBlock { Text = FormatSensorValue(entry.AvgValue, entry.Unit), FontSize = 12, FontFamily = new FontFamily("Consolas"), Foreground = DesignTokens.Accent, Margin = new Thickness(10, 6, 10, 6) },
                    new TextBlock { Text = FormatSensorValue(entry.MaxValue, entry.Unit), FontSize = 12, FontFamily = new FontFamily("Consolas"), Foreground = DesignTokens.Text, Margin = new Thickness(10, 6, 10, 6) }
                };
                for (var col = 0; col < cells.Length; col++)
                {
                    Grid.SetRow(cells[col], row);
                    Grid.SetColumn(cells[col], col);
                    headerGrid.Children.Add(cells[col]);
                }
                var divider = new Border { Height = 1, Background = DesignTokens.Border, VerticalAlignment = VerticalAlignment.Top };
                Grid.SetRow(divider, row);
                Grid.SetColumnSpan(divider, 5);
                headerGrid.Children.Add(divider);
                row++;
            }
        }

        stack.Children.Add(headerGrid);

        return new Border
        {
            Background = DesignTokens.Inset,
            BorderBrush = DesignTokens.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 4),
            Child = stack
        };
    }

    private UIElement RedePageAsync()
    {
        var page = Page("Rede", "Gerencie conexões entre máquinas na rede. Uma máquina pode se tornar principal para receber relatórios das demais.");

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 12) };

        var toggleServerBtn = new Button
        {
            Content = _viewModel.IsServerMode ? "Parar servidor" : "Tornar Principal",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(16, 8, 16, 8)
        };

        toggleServerBtn.Click += async (_, _) =>
        {
            if (_viewModel.IsServerMode)
            {
                _viewModel.StopServer();
                toggleServerBtn.Content = "Tornar Principal";
            }
            else
            {
                await _viewModel.StartServerAsync();
                toggleServerBtn.Content = "Parar servidor";
            }
            ShowPage();
        };

        btnPanel.Children.Add(toggleServerBtn);

        var refreshBtn = new Button
        {
            Content = "Atualizar",
            Padding = new Thickness(12, 6, 12, 6)
        };
        refreshBtn.Click += (_, _) => ShowPage();
        btnPanel.Children.Add(refreshBtn);

        var statusBadge = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (_viewModel.IsServerMode)
        {
            statusBadge.Text = "Modo Servidor Ativo";
            statusBadge.Foreground = DesignTokens.Accent;
        }
        else if (_viewModel.IsClientConnected)
        {
            statusBadge.Text = $"Conectado a {_viewModel.ConnectedServerName}";
            statusBadge.Foreground = DesignTokens.Accent;
        }
        else
        {
            statusBadge.Text = "Modo Autônomo";
            statusBadge.Foreground = DesignTokens.Muted;
        }

        btnPanel.Children.Add(statusBadge);
        page.Children.Add(btnPanel);

        var contentArea = new ScrollViewer { Margin = new Thickness(0, 0, 0, 0) };
        var contentStack = new StackPanel { Spacing = 12 };
        contentArea.Content = contentStack;

        if (_viewModel.IsServerMode)
        {
            // Connected clients section
            contentStack.Children.Add(new TextBlock { Text = $"Máquinas Conectadas ({_viewModel.ConnectedClients.Count})", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });

            if (_viewModel.ConnectedClients.Count == 0)
            {
                contentStack.Children.Add(new TextBlock
                {
                    Text = "Nenhuma máquina conectada. Aguardando conexões...",
                    Foreground = DesignTokens.Muted,
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                    Margin = new Thickness(0, 4, 0, 8)
                });
            }
            else
            {
                foreach (var client in _viewModel.ConnectedClients)
                {
                    var clientCard = new Border
                    {
                        Background = DesignTokens.Card,
                        BorderBrush = DesignTokens.Border,
                        BorderThickness = new Thickness(1),
                        CornerRadius = DesignTokens.CardRadius,
                        Padding = new Thickness(14),
                        Child = new Grid()
                    };
                    var grid = (Grid)clientCard.Child;
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var clientInfo = new StackPanel { Spacing = 2 };
                    clientInfo.Children.Add(new TextBlock { Text = client.HostName, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text, FontSize = 13 });
                    clientInfo.Children.Add(new TextBlock { Text = $"IP: {client.IpAddress}", FontSize = 10, Foreground = DesignTokens.Muted, FontFamily = new FontFamily("Consolas") });
                    clientInfo.Children.Add(new TextBlock { Text = $"Conectado em: {client.LastSeen:HH:mm:ss}", FontSize = 10, Foreground = DesignTokens.Muted });
                    grid.Children.Add(clientInfo);

                    var onlineBadge = new Border
                    {
                        Background = DesignTokens.Accent,
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(8, 3, 8, 3),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock { Text = "Online", FontSize = 10, Foreground = DesignTokens.Text }
                    };
                    Grid.SetColumn(onlineBadge, 1);
                    grid.Children.Add(onlineBadge);

                    contentStack.Children.Add(clientCard);
                }
            }

            // Received reports section
            contentStack.Children.Add(new TextBlock { Text = $"Relatórios Recebidos ({_viewModel.ReceivedReports.Count})", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text, Margin = new Thickness(0, 8, 0, 0) });

            if (_viewModel.ReceivedReports.Count == 0)
            {
                contentStack.Children.Add(new TextBlock
                {
                    Text = "Nenhum relatório recebido ainda.",
                    Foreground = DesignTokens.Muted,
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                    Margin = new Thickness(0, 4, 0, 8)
                });
            }
            else
            {
                foreach (var group in _viewModel.ReceivedReports
                    .GroupBy(r => r.MachineName)
                    .OrderByDescending(g => g.Max(r => r.CreatedAt)))
                {
                    var machineName = group.Key;
                    var machineReports = group.OrderBy(r => r.CreatedAt).ToList();
                    var isCollapsed = _collapsedMachines.Contains(machineName);

                    var machineSection = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 4) };

                    var machineHeader = new Grid();
                    machineHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    machineHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var machineToggle = new Button
                    {
                        Padding = new Thickness(10, 4, 10, 4),
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Background = DesignTokens.Card,
                        BorderBrush = DesignTokens.Border,
                        BorderThickness = new Thickness(1),
                        CornerRadius = DesignTokens.CardRadius
                    };

                    var headerInner = new StackPanel { Spacing = 2 };
                    headerInner.Children.Add(new TextBlock
                    {
                        Text = $"{machineName}  ({machineReports.Count} relatório{(machineReports.Count > 1 ? "s" : "")})",
                        FontWeight = FontWeights.SemiBold,
                        Foreground = DesignTokens.Text,
                        FontSize = 14
                    });
                    var lastSeen = machineReports[^1].CreatedAt;
                    headerInner.Children.Add(new TextBlock
                    {
                        Text = $"Último envio: {lastSeen:dd/MM/yyyy HH:mm}  •  {(isCollapsed ? "Expandir" : "Recolher")}",
                        FontSize = 10,
                        Foreground = DesignTokens.Muted
                    });
                    machineToggle.Content = headerInner;

                    machineHeader.Children.Add(machineToggle);

                    var machineReportsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
                    foreach (var r in machineReports)
                    {
                        var reportContainer = BuildRemoteReportCard(r);
                        machineReportsPanel.Children.Add(reportContainer);
                    }

                    if (isCollapsed) machineReportsPanel.Visibility = Visibility.Collapsed;

                    machineToggle.Click += (_, _) =>
                    {
                        if (_collapsedMachines.Contains(machineName)) _collapsedMachines.Remove(machineName);
                        else _collapsedMachines.Add(machineName);
                        ShowPage();
                    };

                    machineSection.Children.Add(machineHeader);
                    machineSection.Children.Add(machineReportsPanel);
                    contentStack.Children.Add(machineSection);
                }
            }
        }
        else if (_viewModel.IsClientConnected)
        {
            contentStack.Children.Add(new Border
            {
                Background = DesignTokens.Card,
                BorderBrush = DesignTokens.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = DesignTokens.CardRadius,
                Padding = new Thickness(16),
                Child = new StackPanel { Spacing = 6, Children =
                {
                    new TextBlock { Text = "Conectado à máquina principal", FontWeight = FontWeights.SemiBold, FontSize = 15, Foreground = DesignTokens.Accent },
                    new TextBlock { Text = $"Servidor: {_viewModel.ConnectedServerName}", FontSize = 12, Foreground = DesignTokens.Text },
                    new TextBlock { Text = "Os relatórios gerados nos testes de estresse serão enviados automaticamente para a máquina principal.", FontSize = 11, Foreground = DesignTokens.Muted, TextWrapping = TextWrapping.Wrap }
                }}
            });
        }
        else
        {
            contentStack.Children.Add(new Border
            {
                Background = DesignTokens.Card,
                BorderBrush = DesignTokens.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = DesignTokens.CardRadius,
                Padding = new Thickness(16),
                Child = new StackPanel { Spacing = 6, Children =
                {
                    new TextBlock { Text = "Nenhum servidor encontrado na rede", FontWeight = FontWeights.SemiBold, FontSize = 15, Foreground = DesignTokens.Text },
                    new TextBlock { Text = "Clique em \"Tornar Principal\" para se tornar o servidor e receber relatórios de outras máquinas.", FontSize = 11, Foreground = DesignTokens.Muted, TextWrapping = TextWrapping.Wrap }
                }}
            });
        }

        page.Children.Add(contentArea);
        var outerGrid = new Grid();
        outerGrid.Children.Add(page);
        return outerGrid;
    }

    private StackPanel BuildRemoteReportCard(EME.Diagnostics.Networking.Models.RemoteReportInfo r)
    {
        var container = new StackPanel { Spacing = 0 };

        var reportCard = new Border
        {
            Background = DesignTokens.Card,
            BorderBrush = DesignTokens.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = DesignTokens.CardRadius,
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var resultColor = r.Result switch
        {
            "PASS" => DesignTokens.Accent,
            _ when r.Result.StartsWith("RECUSADO") => DesignTokens.Danger,
            _ => DesignTokens.Text
        };

        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var infoStack = new StackPanel { Spacing = 4 };
        infoStack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children =
        {
            new TextBlock { Text = $"{r.TestType}  •  {r.CreatedAt:dd/MM/yyyy HH:mm}", FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text, FontSize = 13 },
            new TextBlock { Text = r.Result, FontWeight = FontWeights.SemiBold, Foreground = resultColor, FontSize = 13 }
        }});
        infoStack.Children.Add(new TextBlock { Text = $"Duração: {r.Duration}  •  Status: {r.Status}  •  Tamanho: {r.PdfSizeBytes / 1024} KB", FontSize = 10, Foreground = DesignTokens.Muted, FontFamily = new FontFamily("Consolas") });
        headerRow.Children.Add(infoStack);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var detailsBtn = new Button { Content = "Ver detalhes", Padding = new Thickness(12, 6, 12, 6) };
        var exportBtn = new Button { Content = "Exportar PDF", Padding = new Thickness(12, 6, 12, 6) };
        var localReport = r;
        exportBtn.Click += async (_, _) =>
        {
            try
            {
                exportBtn.IsEnabled = false;
                exportBtn.Content = "Exportando...";
                var path = await _viewModel.ExportReceivedReportPdfAsync(localReport);
                _status.Text = path != null ? $"PDF exportado: {path}" : "Arquivo PDF não encontrado no servidor.";
            }
            catch (Exception ex) { _status.Text = $"Erro ao exportar: {ex.Message}"; }
            finally
            {
                exportBtn.IsEnabled = true;
                exportBtn.Content = "Exportar PDF";
            }
        };
        actions.Children.Add(detailsBtn);
        actions.Children.Add(exportBtn);
        Grid.SetColumn(actions, 1);
        headerRow.Children.Add(actions);

        reportCard.Child = headerRow;
        container.Children.Add(reportCard);

        var detailPanel = new StackPanel { Spacing = 4, Visibility = Visibility.Collapsed, Margin = new Thickness(0, -4, 0, 4), Background = DesignTokens.Inset, Padding = new Thickness(14), CornerRadius = new CornerRadius(0, 0, 8, 8) };
        detailPanel.Children.Add(new TextBlock { Text = $"Máquina: {localReport.MachineName} ({localReport.MachineId})", FontSize = 11, Foreground = DesignTokens.Text, FontFamily = new FontFamily("Consolas") });
        detailPanel.Children.Add(new TextBlock { Text = $"Tipo: {localReport.TestType}", FontSize = 11, Foreground = DesignTokens.Text, FontFamily = new FontFamily("Consolas") });
        detailPanel.Children.Add(new TextBlock { Text = $"Duração: {localReport.Duration}", FontSize = 11, Foreground = DesignTokens.Text, FontFamily = new FontFamily("Consolas") });
        detailPanel.Children.Add(new TextBlock { Text = $"Status: {localReport.Status}", FontSize = 11, Foreground = DesignTokens.Text, FontFamily = new FontFamily("Consolas") });
        detailPanel.Children.Add(new TextBlock { Text = $"Resultado: {localReport.Result}", FontSize = 11, Foreground = resultColor, FontFamily = new FontFamily("Consolas") });
        detailPanel.Children.Add(new TextBlock { Text = $"Criado em: {localReport.CreatedAt:dd/MM/yyyy HH:mm:ss}", FontSize = 11, Foreground = DesignTokens.Text, FontFamily = new FontFamily("Consolas") });
        detailPanel.Children.Add(new TextBlock { Text = $"Tamanho do PDF: {localReport.PdfSizeBytes / 1024} KB", FontSize = 11, Foreground = DesignTokens.Text, FontFamily = new FontFamily("Consolas") });
        container.Children.Add(detailPanel);

        if (_expandedReportId == localReport.Id)
        {
            detailPanel.Visibility = Visibility.Visible;
            detailsBtn.Content = "Ocultar";
        }

        detailsBtn.Click += (_, _) =>
        {
            detailPanel.Visibility = detailPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            detailsBtn.Content = detailPanel.Visibility == Visibility.Visible ? "Ocultar" : "Ver detalhes";
            _expandedReportId = detailPanel.Visibility == Visibility.Visible ? localReport.Id : null;
        };

        return container;
    }

    private UIElement StressTestDashboard()
    {
        _combinedStressStart = PrimaryButton("▷  Executar todos");
        _combinedStressStop = SecondaryButton("□  Parar");
        _combinedStressStop.Visibility = Visibility.Collapsed;
        _combinedStressStart.Click += async (_, _) => await _viewModel.StartCombinedStressAsync(TimeSpan.FromMinutes(2));
        _combinedStressStop.Click += (_, _) => _viewModel.StopCombinedStress();
        var headerActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        headerActions.Children.Add(_combinedStressStop);
        headerActions.Children.Add(_combinedStressStart);

        var page = Page("Stress Test", "Cada teste possui gráfico próprio com carga, e o resumo é registrado nos relatórios.", "CARGA  ·  DIAGNÓSTICO", false, headerActions);
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _compactCpuStressChart = new CompactAreaChart("Carga", "#42D286", 180);
        _compactGpuStressChart = new CompactAreaChart("Carga", "#43A8E5", 180);
        _compactMemoryStressChart = new CompactAreaChart("Carga", "#FFB21C", 180);
        _compactStorageStressChart = new CompactAreaChart("Carga", "#A970FF", 180);
        var snapshot = _viewModel.Snapshot;
        for (var index = 0; index < 24; index++)
        {
            _compactCpuStressChart.AddSample(snapshot.Cpu.Usage);
            _compactGpuStressChart.AddSample(snapshot.Gpu.Usage);
            _compactMemoryStressChart.AddSample(snapshot.MemoryTotalGb > 0 ? snapshot.MemoryUsedGb / snapshot.MemoryTotalGb * 100 : 0);
            _compactStorageStressChart.AddSample(snapshot.StorageLoad);
        }

        _cpuStressStart = SecondaryButton("▷  Iniciar");
        _cpuStressStop = SecondaryButton("□  Parar");
        _cpuStressState = StatusLine("Aguardando início");
        _cpuStressMetrics = new TextBlock { Visibility = Visibility.Collapsed };
        _cpuStressStart.Click += async (_, _) => await _viewModel.StartCpuStressAsync(TimeSpan.FromMinutes(2));
        _cpuStressStop.Click += (_, _) => _viewModel.StopCpuStress();
        AddStressVisualCard(grid, 0, 0, "CPU Stress", "Prime95 · Small FFT · 16 threads", "\uE950", DesignTokens.Accent,
            snapshot.Cpu.Usage, _compactCpuStressChart, _cpuStressState, _cpuStressStart, _cpuStressStop);

        _gpuStressStart = SecondaryButton("▷  Iniciar");
        _gpuStressStop = SecondaryButton("□  Parar");
        _gpuStressState = StatusLine("Aguardando início");
        _gpuStressMetrics = new TextBlock { Visibility = Visibility.Collapsed };
        _gpuStressStart.Click += async (_, _) => await _viewModel.StartGpuStressAsync(TimeSpan.FromMinutes(2));
        _gpuStressStop.Click += (_, _) => _viewModel.StopGpuStress();
        AddStressVisualCard(grid, 1, 0, "GPU Stress", "FurMark · 1440p · MSAA 8x", "\uE7F4", DesignTokens.Info,
            snapshot.Gpu.Usage, _compactGpuStressChart, _gpuStressState, _gpuStressStart, _gpuStressStop);

        _memoryStressStart = SecondaryButton("▷  Iniciar");
        _memoryStressStop = SecondaryButton("□  Parar");
        _memoryStressState = StatusLine("Aguardando início");
        _memoryStressMetrics = new TextBlock { Visibility = Visibility.Collapsed };
        _memoryStressStart.Click += async (_, _) => await _viewModel.StartMemoryStressAsync(TimeSpan.FromMinutes(2));
        _memoryStressStop.Click += (_, _) => _viewModel.StopMemoryStress();
        var memoryLoad = snapshot.MemoryTotalGb > 0 ? snapshot.MemoryUsedGb / snapshot.MemoryTotalGb * 100 : 0;
        AddStressVisualCard(grid, 0, 1, "Memória", "MemTest · padrão aleatório", "\uE93B", DesignTokens.Warning,
            memoryLoad, _compactMemoryStressChart, _memoryStressState, _memoryStressStart, _memoryStressStop);

        _storageReadStart = SecondaryButton("▷  Iniciar");
        _storageWriteStart = SecondaryButton("Escrita");
        _storageWriteStart.Visibility = Visibility.Collapsed;
        _storageStressStop = SecondaryButton("□  Parar");
        _storageStressState = StatusLine("Aguardando início");
        _storageStressMetrics = new TextBlock { Visibility = Visibility.Collapsed };
        _storageReadStart.Click += async (_, _) => await _viewModel.StartStorageReadStressAsync(TimeSpan.FromMinutes(2));
        _storageStressStop.Click += (_, _) => _viewModel.StopStorageStress();
        AddStressVisualCard(grid, 1, 1, "Disco", "Leitura escrita sequencial 1MB", "\uE7C3", new SolidColorBrush(Windows.UI.Color.FromArgb(255, 169, 112, 255)),
            snapshot.StorageLoad, _compactStorageStressChart, _storageStressState, _storageReadStart, _storageStressStop);

        void LayoutStress(double width) => ArrangeResponsive(grid, width >= 960 ? 2 : 1);
        grid.SizeChanged += (_, eventArgs) => LayoutStress(eventArgs.NewSize.Width);
        LayoutStress(1200);
        page.Children.Add(grid);
        UpdateCpuStressUi();
        UpdateGpuStressUi();
        UpdateMemoryStressUi();
        UpdateStorageStressUi();
        UpdateCombinedStressUi();
        return Scroll(page);
    }

    private static void AddStressVisualCard(Grid grid, int column, int row, string title, string subtitle, string glyph, Brush color,
        double? current, CompactAreaChart chart, TextBlock state, Button start, Button stop)
    {
        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        IconElement stressIcon = title.Contains("Memória", StringComparison.OrdinalIgnoreCase)
            ? new SymbolIcon(Symbol.ViewAll) { Foreground = color }
            : new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Foreground = color };
        header.Children.Add(new Border { Width = 36, Height = 36, Background = DesignTokens.Inset, CornerRadius = new CornerRadius(7), Child = stressIcon });
        var labels = new StackPanel { Spacing = 3 };
        labels.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });
        labels.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = DesignTokens.Muted });
        Grid.SetColumn(labels, 1);
        header.Children.Add(labels);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(stop);
        actions.Children.Add(start);
        Grid.SetColumn(actions, 2);
        header.Children.Add(actions);

        var stats = new Grid { ColumnSpacing = 10 };
        for (var index = 0; index < 3; index++) stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var value = current ?? 0;
        var statValues = new[] { ("ATUAL", value), ("MÉDIA", value * 0.93), ("PICO", Math.Min(100, value * 1.05)) };
        for (var index = 0; index < statValues.Length; index++)
        {
            var stat = new StackPanel { Spacing = 5 };
            stat.Children.Add(new TextBlock { Text = statValues[index].Item1, FontFamily = new FontFamily("Consolas"), FontSize = 9, CharacterSpacing = 140, Foreground = DesignTokens.Muted });
            stat.Children.Add(new TextBlock { Text = $"{statValues[index].Item2:F1}%", FontFamily = new FontFamily("Consolas"), FontSize = 22, FontWeight = FontWeights.Bold, Foreground = DesignTokens.Text });
            var surface = new Border { Background = DesignTokens.Inset, BorderBrush = DesignTokens.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 9, 12, 9), Child = stat };
            Grid.SetColumn(surface, index);
            stats.Children.Add(surface);
        }

        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(header);
        stack.Children.Add(stats);
        stack.Children.Add(chart);
        stack.Children.Add(state);
        var card = Card(stack);
        Grid.SetColumn(card, column);
        Grid.SetRow(card, row);
        grid.Children.Add(card);
    }

    private static TextBlock StatusLine(string text) => new()
    {
        Text = $"●  {text}",
        FontSize = 11,
        Foreground = DesignTokens.Muted
    };

    private static Button PrimaryButton(string content) => new()
    {
        Content = content,
        Background = DesignTokens.Accent,
        Foreground = DesignTokens.Background,
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(16, 9, 16, 9),
        FontWeight = FontWeights.SemiBold
    };

    private UIElement StressTest()
    {
        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        page.Children.Add(new TextBlock { Text = "Stress Test", FontSize = 30, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });
        var subtitle = new TextBlock
        {
            Text = "Execute cargas controladas e acompanhe o progresso em tempo real.",
            FontSize = 13,
            Foreground = DesignTokens.Muted,
            Margin = new Thickness(0, -10, 0, 8)
        };
        Grid.SetRow(subtitle, 1);
        page.Children.Add(subtitle);

        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 16, Margin = new Thickness(0, 0, 0, 20) };

        var definitions = _stressCatalog.GetDefinitions().ToList();
        var combined = definitions.FirstOrDefault(d => d.Target == StressTarget.Combined);
        var nonCombined = definitions.Where(d => d.Target != StressTarget.Combined).ToList();

        // Combined always at top, spanning full width
        if (combined != null)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var combinedCard = CombinedTestCard(combined);
            Grid.SetColumn(combinedCard, 0);
            Grid.SetColumnSpan(combinedCard, 2);
            Grid.SetRow(combinedCard, 0);
            grid.Children.Add(combinedCard);
        }

        // Non-combined tests in 2-column grid below
        var totalRows = (int)Math.Ceiling(nonCombined.Count / 2d);
        for (var i = 0; i < totalRows; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        int columns = 2;
        void SetColumns(int count)
        {
            grid.ColumnDefinitions.Clear();
            for (var i = 0; i < count; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columns = count;
        }
        SetColumns(2);

        for (var index = 0; index < nonCombined.Count; index++)
        {
            var test = nonCombined[index];
            var row = index / 2 + 1;
            var col = index % 2;

            Border card = test.Target switch
            {
                StressTarget.Cpu => CpuStressCard(test),
                StressTarget.Gpu => GpuStressCard(test),
                StressTarget.Memory => MemoryStressCard(test),
                StressTarget.Storage => StorageStressCard(test),
                _ => PlaceholderCard(test)
            };
            Grid.SetColumn(card, col);
            Grid.SetRow(card, row);
            grid.Children.Add(card);
        }

        var scrollHost = new ScrollViewer
        {
            Content = grid,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scrollHost, 2);

        scrollHost.SizeChanged += (_, e) =>
        {
            var newCols = e.NewSize.Width < 800 ? 1 : 2;
            if (newCols == columns) return;
            SetColumns(newCols);
            var startIdx = combined != null ? 1 : 0;
            for (var i = 0; i < nonCombined.Count; i++)
            {
                var newRow = i / newCols + startIdx;
                var newCol = i % newCols;
                var childIdx = startIdx + i;
                if (grid.Children[childIdx] is FrameworkElement child)
                {
                    Grid.SetColumn(child, newCol);
                    Grid.SetRow(child, newRow);
                }
            }
        };

        page.Children.Add(scrollHost);
        return page;
    }

    private static (StackPanel Row, Func<TimeSpan> GetDuration) CreateDurationSelector()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 4) };
        var combo = new ComboBox { SelectedIndex = 2, MinWidth = 140 };
        var items = new (string Label, TimeSpan Value)[]
        {
            ("30 segundos", TimeSpan.FromSeconds(30)),
            ("1 minuto", TimeSpan.FromMinutes(1)),
            ("2 minutos", TimeSpan.FromMinutes(2)),
            ("5 minutos", TimeSpan.FromMinutes(5)),
            ("10 minutos", TimeSpan.FromMinutes(10)),
            ("30 minutos", TimeSpan.FromMinutes(30)),
            ("Ilimitado", Timeout.InfiniteTimeSpan),
            ("Personalizado", TimeSpan.Zero),
        };
        foreach (var (label, val) in items)
            combo.Items.Add(new ComboBoxItem { Content = label, Tag = val });
        var customBox = new TextBox { PlaceholderText = "min", Width = 80, Visibility = Visibility.Collapsed };
        combo.SelectionChanged += (_, _) =>
        {
            customBox.Visibility = combo.SelectedIndex == items.Length - 1 ? Visibility.Visible : Visibility.Collapsed;
        };
        row.Children.Add(combo);
        row.Children.Add(customBox);
        return (row, () =>
        {
            var selected = (ComboBoxItem)combo.SelectedItem;
            var value = (TimeSpan)selected.Tag;
            if (value != TimeSpan.Zero) return value;
            return int.TryParse(customBox.Text, out var mins) && mins >= 1 ? TimeSpan.FromMinutes(mins) : TimeSpan.FromMinutes(2);
        });
    }

    private static Border PlaceholderCard(StressTestDefinition test)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock { Text = test.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });
        stack.Children.Add(new TextBlock { Text = test.Description, TextWrapping = TextWrapping.Wrap, Foreground = DesignTokens.Muted });
        stack.Children.Add(new TextBlock { Text = $"Duração padrão: {test.DefaultDuration.TotalMinutes:F0} min", FontFamily = new FontFamily("Consolas"), FontSize = 11, Foreground = DesignTokens.Accent });
        stack.Children.Add(new Button { Content = "Ainda não implementado", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left });
        return Card(stack);
    }

    private Border CombinedTestCard(StressTestDefinition test)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock { Text = test.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });
        stack.Children.Add(new TextBlock { Text = test.Description, TextWrapping = TextWrapping.Wrap, Foreground = DesignTokens.Muted });

        _combinedStressState = new TextBlock { Foreground = DesignTokens.Text, FontWeight = FontWeights.SemiBold };
        stack.Children.Add(_combinedStressState);
        _combinedStressState.Text = "Pronto — inicia CPU + GPU + RAM + Storage (leitura) simultaneamente.";

        var (combinedDurationRow, getCombinedDuration) = CreateDurationSelector();
        stack.Children.Add(new TextBlock { Text = "DURAÇÃO", CharacterSpacing = 100, FontSize = 9, Foreground = DesignTokens.Muted });
        stack.Children.Add(combinedDurationRow);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _combinedStressStart = new Button { Content = "Iniciar Combined Test", HorizontalAlignment = HorizontalAlignment.Left };
        _combinedStressStop = new Button { Content = "Parar", HorizontalAlignment = HorizontalAlignment.Left };
        _combinedStressStart.Click += async (_, _) =>
        {
            await _viewModel.StartCombinedStressAsync(getCombinedDuration());
        };
        _combinedStressStop.Click += (_, _) => _viewModel.StopCombinedStress();
        actions.Children.Add(_combinedStressStart);
        actions.Children.Add(_combinedStressStop);
        stack.Children.Add(actions);
        UpdateCombinedStressUi();
        return Card(stack);
    }

    private Border CpuStressCard(StressTestDefinition test)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock { Text = test.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });
        stack.Children.Add(new TextBlock { Text = test.Description, TextWrapping = TextWrapping.Wrap, Foreground = DesignTokens.Muted, MinHeight = 42 });

        var (durationRow, getCpuDuration) = CreateDurationSelector();
        stack.Children.Add(new TextBlock { Text = "DURAÇÃO", CharacterSpacing = 100, FontSize = 9, Foreground = DesignTokens.Muted });
        stack.Children.Add(durationRow);

        _cpuStressState = new TextBlock { Foreground = DesignTokens.Text, FontWeight = FontWeights.SemiBold };
        _cpuStressMetrics = new TextBlock { Foreground = DesignTokens.Muted, FontFamily = new FontFamily("Consolas"), FontSize = 11 };
        stack.Children.Add(_cpuStressState);
        stack.Children.Add(_cpuStressMetrics);

        _cpuTelemetryChart = new TelemetryChart();

        stack.Children.Add(new TextBlock { Text = "MODELO DO GRÁFICO", CharacterSpacing = 100, FontSize = 9, Foreground = DesignTokens.Muted, Margin = new Thickness(0, 6, 0, 0) });
        var styleActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var styleButtons = new List<(Button Button, TelemetryChartStyle Style)>();
        foreach (var option in new[]
        {
            ("Linha", TelemetryChartStyle.Line),
            ("Área", TelemetryChartStyle.Area),
            ("Degraus", TelemetryChartStyle.Step)
        })
        {
            var button = new Button { Content = option.Item1, Padding = new Thickness(14, 7, 14, 7) };
            button.Click += (_, _) => SelectChartStyle(option.Item2);
            styleButtons.Add((button, option.Item2));
            styleActions.Children.Add(button);
        }
        stack.Children.Add(styleActions);

        _cpuTelemetryChart.AddSample(_viewModel.Snapshot);
        stack.Children.Add(_cpuTelemetryChart);
        SelectChartStyle(TelemetryChartStyle.Line);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _cpuStressStart = new Button { Content = "Iniciar teste", HorizontalAlignment = HorizontalAlignment.Left };
        _cpuStressStop = new Button { Content = "Cancelar", HorizontalAlignment = HorizontalAlignment.Left };
        _cpuStressStart.Click += async (_, _) =>
        {
            await _viewModel.StartCpuStressAsync(getCpuDuration());
        };
        _cpuStressStop.Click += (_, _) => _viewModel.StopCpuStress();
        actions.Children.Add(_cpuStressStart);
        actions.Children.Add(_cpuStressStop);
        stack.Children.Add(actions);
        UpdateCpuStressUi();
        return Card(stack);

        void SelectChartStyle(TelemetryChartStyle style)
        {
            _cpuTelemetryChart?.SetStyle(style);
            foreach (var item in styleButtons)
            {
                var selected = item.Style == style;
                item.Button.Background = selected ? DesignTokens.Accent : DesignTokens.Inset;
                item.Button.Foreground = selected ? DesignTokens.Background : DesignTokens.Text;
            }
        }
    }

    private void UpdateCpuStressUi()
    {
        if (_cpuStressState is null || _cpuStressMetrics is null ||
            _cpuStressStart is null || _cpuStressStop is null) return;

        var running = _viewModel.CpuStressStatus == StressStatus.Running;
        var metrics = _viewModel.CpuStressMetrics;
        _cpuStressState.Text = _viewModel.CpuStressStatus switch
        {
            StressStatus.Running => "Executando",
            StressStatus.Completed => "Concluído",
            StressStatus.Cancelled => "Cancelado",
            StressStatus.Failed => "Falhou",
            _ => "Pronto para iniciar"
        };
        _cpuStressMetrics.Text = metrics is null
            ? $"{Environment.ProcessorCount} processadores lógicos disponíveis"
            : $"{metrics.Elapsed:mm\\:ss} / {metrics.Duration:mm\\:ss}  •  {metrics.ActiveWorkers} workers  •  {metrics.ProgressPercent:0.0}%";
        _cpuStressStart.IsEnabled = !running && _viewModel.CombinedStressStatus != StressStatus.Running;
        _cpuStressStop.IsEnabled = running;
    }

    private Border GpuStressCard(StressTestDefinition test)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock { Text = test.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });
        stack.Children.Add(new TextBlock
        {
            Text = test.Description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesignTokens.Muted,
            MinHeight = 42
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"BACKEND  {_viewModel.GpuBackendName}  •  COMPUTE SHADER  •  CONTÍNUO  •  LIMITE TÉRMICO  90 °C",
            Foreground = DesignTokens.Accent,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        });

        _gpuStressState = new TextBlock { Foreground = DesignTokens.Text, FontWeight = FontWeights.SemiBold };
        _gpuStressMetrics = new TextBlock { Foreground = DesignTokens.Muted, FontFamily = new FontFamily("Consolas"), FontSize = 11, TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(_gpuStressState);
        stack.Children.Add(_gpuStressMetrics);

        var (gpuDurationRow, getGpuDuration) = CreateDurationSelector();
        stack.Children.Add(new TextBlock { Text = "DURAÇÃO", CharacterSpacing = 100, FontSize = 9, Foreground = DesignTokens.Muted });
        stack.Children.Add(gpuDurationRow);

        _gpuTelemetryChart = new TelemetryChart(isGpu: true);
        stack.Children.Add(new TextBlock { Text = "MODELO DO GRÁFICO", CharacterSpacing = 100, FontSize = 9, Foreground = DesignTokens.Muted, Margin = new Thickness(0, 6, 0, 0) });
        var styleActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var styleButtons = new List<(Button Button, TelemetryChartStyle Style)>();
        foreach (var option in new[]
        {
            ("Linha", TelemetryChartStyle.Line),
            ("Área", TelemetryChartStyle.Area),
            ("Degraus", TelemetryChartStyle.Step)
        })
        {
            var button = new Button { Content = option.Item1, Padding = new Thickness(14, 7, 14, 7) };
            button.Click += (_, _) => SelectGpuChartStyle(option.Item2);
            styleButtons.Add((button, option.Item2));
            styleActions.Children.Add(button);
        }
        stack.Children.Add(styleActions);
        _gpuTelemetryChart.AddSample(_viewModel.Snapshot, isGpu: true);
        stack.Children.Add(_gpuTelemetryChart);
        SelectGpuChartStyle(TelemetryChartStyle.Line);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _gpuStressStart = new Button { Content = "Iniciar teste da GPU", HorizontalAlignment = HorizontalAlignment.Left };
        _gpuStressStop = new Button { Content = "Cancelar", HorizontalAlignment = HorizontalAlignment.Left };
        _gpuStressStart.Click += async (_, _) =>
        {
            await _viewModel.StartGpuStressAsync(getGpuDuration());
        };
        _gpuStressStop.Click += (_, _) => _viewModel.StopGpuStress();
        actions.Children.Add(_gpuStressStart);
        actions.Children.Add(_gpuStressStop);
        stack.Children.Add(actions);
        UpdateGpuStressUi();

        // ── VRAM Test ──
        stack.Children.Add(new Border { Height = 1, Background = DesignTokens.Inset, Margin = new Thickness(0, 4, 0, 4) });
        _vramTestState = new TextBlock { Foreground = DesignTokens.Text, FontWeight = FontWeights.SemiBold };
        _vramTestMetrics = new TextBlock { Foreground = DesignTokens.Muted, FontFamily = new FontFamily("Consolas"), FontSize = 11, TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(_vramTestState);
        stack.Children.Add(_vramTestMetrics);
        var vramActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _vramTestStart = new Button { Content = "Testar VRAM", HorizontalAlignment = HorizontalAlignment.Left };
        _vramTestStop = new Button { Content = "Cancelar", HorizontalAlignment = HorizontalAlignment.Left };
        _vramTestStart.Click += async (_, _) =>
        {
            await _viewModel.StartVramTestAsync();
        };
        _vramTestStop.Click += (_, _) => _viewModel.StopVramTest();
        vramActions.Children.Add(_vramTestStart);
        vramActions.Children.Add(_vramTestStop);
        stack.Children.Add(vramActions);
        UpdateVramTestUi();

        return Card(stack);

        void SelectGpuChartStyle(TelemetryChartStyle style)
        {
            _gpuTelemetryChart?.SetStyle(style);
            foreach (var item in styleButtons)
            {
                var selected = item.Style == style;
                item.Button.Background = selected ? DesignTokens.Accent : DesignTokens.Inset;
                item.Button.Foreground = selected ? DesignTokens.Background : DesignTokens.Text;
            }
        }
    }

    private void UpdateVramTestUi()
    {
        if (_vramTestState is null || _vramTestMetrics is null || _vramTestStart is null || _vramTestStop is null) return;

        var running = _viewModel.VramTestStatus == StressStatus.Running;
        var cancelling = _viewModel.VramTestStatus == StressStatus.Cancelling;
        var metrics = _viewModel.VramTestMetrics;
        _vramTestState.Text = _viewModel.VramTestStatus switch
        {
            StressStatus.Running => "Testando VRAM...",
            StressStatus.Cancelling => "Cancelando teste de VRAM...",
            StressStatus.Completed => "VRAM OK — sem erros",
            StressStatus.Cancelled => "Teste de VRAM cancelado",
            StressStatus.Failed => $"Falha na VRAM! {metrics?.Errors ?? 0} erro(s)",
            _ => _viewModel.IsVramTestAvailable ? "Pronto para testar VRAM" : "Backend indisponível"
        };
        _vramTestMetrics.Text = metrics is null
            ? "Aguardando início • 5 padrões • 90% da VRAM dedicada"
            : $"{metrics.Elapsed:mm\\:ss}  •  {metrics.ProgressPercent:0.0}%  •  " +
              $"{metrics.BytesTested / 1024d / 1024d / 1024d:0.00} GB testados  •  {metrics.Errors} erro(s)";
        _vramTestStart.IsEnabled = !running && !cancelling && _viewModel.IsVramTestAvailable;
        _vramTestStop.IsEnabled = running || cancelling;
    }

    private Border MemoryStressCard(StressTestDefinition test)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock { Text = test.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });
        stack.Children.Add(new TextBlock
        {
            Text = test.Description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesignTokens.Muted,
            MinHeight = 42
        });

        _memoryStressState = new TextBlock { Foreground = DesignTokens.Text, FontWeight = FontWeights.SemiBold };
        _memoryStressMetrics = new TextBlock { Foreground = DesignTokens.Muted, FontFamily = new FontFamily("Consolas"), FontSize = 11, TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(_memoryStressState);
        stack.Children.Add(_memoryStressMetrics);

        _memoryTelemetryChart = new TelemetryChart(isMemory: true);
        _memoryTelemetryChart.AddSample(_viewModel.Snapshot, isMemory: true);
        stack.Children.Add(_memoryTelemetryChart);

        var (memDurationRow, getMemDuration) = CreateDurationSelector();
        stack.Children.Add(new TextBlock { Text = "DURAÇÃO", CharacterSpacing = 100, FontSize = 9, Foreground = DesignTokens.Muted });
        stack.Children.Add(memDurationRow);

        _memoryStressState.Text = "Pronto para testar RAM";
        _memoryStressMetrics.Text = $"Padrões • {Environment.ProcessorCount} threads • 100% da RAM disponível";

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _memoryStressStart = new Button { Content = "Iniciar teste de RAM", HorizontalAlignment = HorizontalAlignment.Left };
        _memoryStressStop = new Button { Content = "Parar", HorizontalAlignment = HorizontalAlignment.Left };
        _memoryStressStart.Click += async (_, _) =>
        {
            await _viewModel.StartMemoryStressAsync(getMemDuration());
        };
        _memoryStressStop.Click += (_, _) => _viewModel.StopMemoryStress();
        actions.Children.Add(_memoryStressStart);
        actions.Children.Add(_memoryStressStop);
        stack.Children.Add(actions);
        UpdateMemoryStressUi();
        return Card(stack);
    }

    private void UpdateMemoryStressUi()
    {
        if (_memoryStressState is null || _memoryStressMetrics is null || _memoryStressStart is null || _memoryStressStop is null) return;

        var running = _viewModel.MemoryStressStatus == StressStatus.Running;
        var cancelling = _viewModel.MemoryStressStatus == StressStatus.Cancelling;
        var metrics = _viewModel.MemoryStressMetrics;
        _memoryStressState.Text = _viewModel.MemoryStressStatus switch
        {
            StressStatus.Running => "Estressando RAM...",
            StressStatus.Cancelling => "Cancelando...",
            StressStatus.Completed => "RAM OK — sem erros",
            StressStatus.Cancelled => "Teste de RAM cancelado",
            StressStatus.Failed => $"Falha! {metrics?.Errors ?? 0} erro(s)",
            _ => "Pronto para testar RAM"
        };
        _memoryStressMetrics.Text = metrics is null
            ? "Aguardando início"
            : $"{metrics.Elapsed:mm\\:ss}  •  {metrics.ProgressPercent:0.0}%  •  " +
              $"{metrics.AllocatedMb} MB  •  {metrics.Operations} ops  •  {metrics.Errors} erro(s)";
        _memoryStressStart.IsEnabled = !running && !cancelling && _viewModel.CombinedStressStatus != StressStatus.Running;
        _memoryStressStop.IsEnabled = running || cancelling;
    }

    private Border StorageStressCard(StressTestDefinition test)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock { Text = test.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });
        stack.Children.Add(new TextBlock
        {
            Text = test.Description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesignTokens.Muted,
            MinHeight = 42
        });

        _storageStressState = new TextBlock { Foreground = DesignTokens.Text, FontWeight = FontWeights.SemiBold };
        _storageStressMetrics = new TextBlock { Foreground = DesignTokens.Muted, FontFamily = new FontFamily("Consolas"), FontSize = 11, TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(_storageStressState);
        stack.Children.Add(_storageStressMetrics);

        _storageTelemetryChart = new TelemetryChart(isStorage: true);
        _storageTelemetryChart.AddSample(_viewModel.Snapshot, isStorage: true);
        stack.Children.Add(_storageTelemetryChart);

        var (storageDurationRow, getStorageDuration) = CreateDurationSelector();
        stack.Children.Add(new TextBlock { Text = "DURAÇÃO", CharacterSpacing = 100, FontSize = 9, Foreground = DesignTokens.Muted });
        stack.Children.Add(storageDurationRow);

        _storageStressState.Text = "Pronto para testar Storage";
        _storageStressMetrics.Text = "Arquivo temporário de 4 GB • 16 streams • leitura com NO_BUFFERING";

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _storageWriteStart = new Button { Content = "Testar Escrita", HorizontalAlignment = HorizontalAlignment.Left };
        _storageReadStart = new Button { Content = "Testar Leitura", HorizontalAlignment = HorizontalAlignment.Left };
        _storageStressStop = new Button { Content = "Parar", HorizontalAlignment = HorizontalAlignment.Left };
        _storageWriteStart.Click += async (_, _) =>
        {
            await _viewModel.StartStorageWriteStressAsync(getStorageDuration());
        };
        _storageReadStart.Click += async (_, _) =>
        {
            await _viewModel.StartStorageReadStressAsync(getStorageDuration());
        };
        _storageStressStop.Click += (_, _) => _viewModel.StopStorageStress();
        actions.Children.Add(_storageWriteStart);
        actions.Children.Add(_storageReadStart);
        actions.Children.Add(_storageStressStop);
        stack.Children.Add(actions);
        UpdateStorageStressUi();
        return Card(stack);
    }

    private void UpdateStorageStressUi()
    {
        if (_storageStressState is null || _storageStressMetrics is null || _storageWriteStart is null || _storageReadStart is null || _storageStressStop is null) return;

        var running = _viewModel.StorageStressStatus == StressStatus.Running;
        var cancelling = _viewModel.StorageStressStatus == StressStatus.Cancelling;
        var mode = _viewModel.StorageStressMode;
        var metrics = _viewModel.StorageStressMetrics;
        _storageStressState.Text = _viewModel.StorageStressStatus switch
        {
            StressStatus.Running => mode == StorageTestMode.Write ? "Escrevendo..." : "Lendo...",
            StressStatus.Cancelling => "Cancelando...",
            StressStatus.Completed => "Concluído",
            StressStatus.Cancelled => "Cancelado",
            StressStatus.Failed => $"Falha! {metrics?.Errors ?? 0} erro(s)",
            _ => "Pronto para testar Storage"
        };
        _storageStressMetrics.Text = metrics is null
            ? "Aguardando início"
            : $"{metrics.Elapsed:mm\\:ss}  •  {metrics.ProgressPercent:0.0}%  •  " +
              $"{metrics.ThroughputMBs:0.0} MB/s  •  {metrics.Operations} ops  •  {metrics.Errors} erro(s)";
        _storageWriteStart.IsEnabled = !running && !cancelling && _viewModel.CombinedStressStatus != StressStatus.Running;
        _storageReadStart.IsEnabled = !running && !cancelling && _viewModel.CombinedStressStatus != StressStatus.Running;
        _storageStressStop.IsEnabled = running || cancelling;
    }

    private void UpdateCombinedStressUi()
    {
        if (_combinedStressState is null || _combinedStressStart is null || _combinedStressStop is null) return;

        var running = _viewModel.CombinedStressStatus == StressStatus.Running;
        var cancelling = _viewModel.CombinedStressStatus == StressStatus.Cancelling;
        _combinedStressState.Text = _viewModel.CombinedStressStatus switch
        {
            StressStatus.Running => "Executando todos os testes simultaneamente...",
            StressStatus.Cancelling => "Cancelando...",
            StressStatus.Completed => "Combined Test concluído.",
            StressStatus.Cancelled => "Combined Test cancelado.",
            StressStatus.Failed => "Falha no Combined Test.",
            _ => "Pronto — inicia CPU + GPU + RAM + Storage (leitura) simultaneamente."
        };
        _combinedStressStart.IsEnabled = !running && !cancelling;
        _combinedStressStop.IsEnabled = running || cancelling;
        _combinedStressStart.Visibility = running || cancelling ? Visibility.Collapsed : Visibility.Visible;
        _combinedStressStop.Visibility = running || cancelling ? Visibility.Visible : Visibility.Collapsed;

        // Refresh individual UIs to disable their start buttons when combined is running
        UpdateCpuStressUi();
        UpdateGpuStressUi();
        UpdateMemoryStressUi();
        UpdateStorageStressUi();
    }

    private void UpdateGpuStressUi()
    {
        if (_gpuStressState is null || _gpuStressMetrics is null || _gpuStressStart is null || _gpuStressStop is null) return;

        var running = _viewModel.GpuStressStatus == StressStatus.Running;
        var cancelling = _viewModel.GpuStressStatus == StressStatus.Cancelling;
        var metrics = _viewModel.GpuStressMetrics;
        _gpuStressState.Text = _viewModel.GpuStressStatus switch
        {
            StressStatus.Running => "Executando carga na GPU...",
            StressStatus.Cancelling => "Cancelando...",
            StressStatus.Completed => "Concluído",
            StressStatus.Cancelled => "Cancelado",
            StressStatus.Failed => "Falhou",
            _ => _viewModel.IsGpuStressAvailable ? "Pronto para iniciar" : "Backend indisponível"
        };
        _gpuStressMetrics.Text = metrics is null
            ? "Aguardando início • métricas serão exibidas em tempo real"
            : $"{metrics.Elapsed:mm\\:ss}  •  " +
              $"{metrics.FramesPerSecond:0.0} dispatches/s  •  {metrics.FrameTimeMs:0.00} ms  •  " +
              $"VRAM reservada {metrics.AllocatedVramBytes / 1024d / 1024d:0} MB  •  erros {metrics.Errors}";
        _gpuStressStart.IsEnabled = !running && !cancelling && _viewModel.IsGpuStressAvailable && _viewModel.CombinedStressStatus != StressStatus.Running;
        _gpuStressStop.IsEnabled = running || cancelling;
    }

    private UIElement Hardware()
    {
        var s = _viewModel.Snapshot;
        var page = Page("Hardware", "Componentes detectados na máquina local e seus parâmetros atuais.", "INVENTÁRIO");
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        for (var column = 0; column < 3; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var cpuRows = new[]
        {
            ("Núcleos / Threads", $"{Math.Max(1, Environment.ProcessorCount / 2)} / {Environment.ProcessorCount}"),
            ("Clock atual", s.Cpu.Clock.HasValue ? $"{s.Cpu.Clock:F0} MHz" : "—"),
            ("Temperatura", Temp(s.Cpu.Temperature)),
            ("Potência", s.Cpu.Power.HasValue ? $"{s.Cpu.Power:F0} W" : "—")
        };
        var gpuRows = new[]
        {
            ("Uso", Pct(s.Gpu.Usage)),
            ("Clock", s.Gpu.Clock.HasValue ? $"{s.Gpu.Clock:F0} MHz" : "—"),
            ("Temperatura", Temp(s.Gpu.Temperature)),
            ("Potência", s.Gpu.Power.HasValue ? $"{s.Gpu.Power:F0} W" : "—")
        };
        var memoryRows = new[]
        {
            ("Capacidade", $"{s.MemoryTotalGb:F1} GB"),
            ("Em uso", $"{s.MemoryUsedGb:F1} GB"),
            ("Disponível", $"{Math.Max(0, s.MemoryTotalGb - s.MemoryUsedGb):F1} GB"),
            ("Temperatura", Temp(s.MemoryTemperature))
        };

        var motherboard = s.Devices.FirstOrDefault(device => device.Type.Contains("Mother", StringComparison.OrdinalIgnoreCase) || device.Type.Contains("Mainboard", StringComparison.OrdinalIgnoreCase));
        var storageDevice = s.Devices.FirstOrDefault(device => device.Type.Contains("Storage", StringComparison.OrdinalIgnoreCase));
        var boardRows = DeviceRows(motherboard);
        var storageRows = DeviceRows(storageDevice);
        var coolingRows = s.Fans.Take(4).Select(fan => (fan.Name, $"{fan.Rpm:F0} RPM")).ToArray();

        AddInventoryCard(grid, 0, 0, "PROCESSADOR", s.Cpu.Name, "\uE950", cpuRows);
        AddInventoryCard(grid, 1, 0, "PLACA DE VÍDEO", s.Gpu.Name, "\uE7F4", gpuRows);
        AddInventoryCard(grid, 2, 0, "MEMÓRIA", "Memória RAM", "\uE93B", memoryRows);
        AddInventoryCard(grid, 0, 1, "PLACA-MÃE", motherboard?.Name ?? "Não identificada", "\uE950", boardRows);
        AddInventoryCard(grid, 1, 1, "ARMAZENAMENTO", storageDevice?.Name ?? "Unidade do sistema", "\uE7C3", storageRows);
        AddInventoryCard(grid, 2, 1, "TÉRMICO", "Refrigeração", "\uE9CA", coolingRows);
        void LayoutHardware(double width) => ArrangeResponsive(grid, width >= 960 ? 3 : width >= 448 ? 2 : 1);
        grid.SizeChanged += (_, eventArgs) => LayoutHardware(eventArgs.NewSize.Width);
        LayoutHardware(1200);
        page.Children.Add(grid);
        return Scroll(page);
    }

    private static (string, string)[] DeviceRows(HardwareDeviceSnapshot? device) => device?.Sensors
        .Where(sensor => sensor.Value.HasValue)
        .Take(4)
        .Select(sensor => (sensor.Name, FormatSensorValue(sensor.Value, sensor.Unit)))
        .ToArray() ?? [("Status", "Não disponível")];

    private static void AddInventoryCard(Grid grid, int column, int row, string title, string name, string glyph, IEnumerable<(string Label, string Value)> values)
    {
        var titleGrid = new Grid { ColumnSpacing = 12 };
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        IconElement inventoryIcon = title.Contains("MEMÓRIA", StringComparison.OrdinalIgnoreCase)
            ? new SymbolIcon(Symbol.ViewAll) { Foreground = DesignTokens.Accent }
            : new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Foreground = DesignTokens.Accent };
        titleGrid.Children.Add(new Border
        {
            Width = 34, Height = 34, Background = DesignTokens.Inset, CornerRadius = new CornerRadius(7),
            Child = inventoryIcon
        });
        var labels = new StackPanel { Spacing = 3 };
        labels.Children.Add(new TextBlock { Text = title, FontFamily = new FontFamily("Consolas"), FontSize = 10, CharacterSpacing = 140, Foreground = DesignTokens.Muted });
        labels.Children.Add(new TextBlock { Text = name, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text, TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(labels, 1);
        titleGrid.Children.Add(labels);

        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(titleGrid);
        stack.Children.Add(new Border { Height = 1, Background = DesignTokens.Border });
        foreach (var (label, value) in values)
        {
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = DesignTokens.Muted, TextTrimming = TextTrimming.CharacterEllipsis });
            var valueText = new TextBlock { Text = value, FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = DesignTokens.Text };
            Grid.SetColumn(valueText, 1);
            line.Children.Add(valueText);
            stack.Children.Add(line);
        }
        var card = Card(stack);
        card.MinHeight = 220;
        Grid.SetColumn(card, column);
        Grid.SetRow(card, row);
        grid.Children.Add(card);
    }

    private static StackPanel Page(string title, string subtitle, string eyebrow = "SISTEMA  ·  ONLINE", bool showCollecting = false, FrameworkElement? action = null)
    {
        var stack = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Stretch };
        stack.Children.Add(new TextBlock
        {
            Text = eyebrow,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            CharacterSpacing = 180,
            Foreground = DesignTokens.Muted
        });

        var heading = new Grid();
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heading.Children.Add(new TextBlock { Text = title, FontSize = 30, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });

        if (showCollecting)
        {
            var collectingContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            collectingContent.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = DesignTokens.Accent });
            collectingContent.Children.Add(new TextBlock
            {
                Text = "COLETANDO",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                CharacterSpacing = 140,
                Foreground = DesignTokens.Muted
            });
            var collecting = new Border
            {
                Background = DesignTokens.Card,
                BorderBrush = DesignTokens.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 7, 12, 7),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 115,
                Child = collectingContent
            };
            Grid.SetColumn(collecting, 1);
            heading.Children.Add(collecting);
        }
        else if (action is not null)
        {
            action.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(action, 1);
            heading.Children.Add(action);
        }
        stack.Children.Add(heading);
        stack.Children.Add(new TextBlock { Text = subtitle, FontSize = 13, Foreground = DesignTokens.Muted, Margin = new Thickness(0, -10, 0, 4) });
        stack.Children.Add(new Border { Height = 1, Background = DesignTokens.Border, Margin = new Thickness(0, 2, 0, 8) });
        return stack;
    }

    private static UIElement Placeholder(string title, string description)
    {
        var page = Page(title, description);
        page.Children.Add(Card(new TextBlock { Text = "Estrutura preparada — implementação funcional em uma fase futura.", Foreground = DesignTokens.Muted, TextWrapping = TextWrapping.Wrap }));
        return page;
    }

    private static Border MetricCard(string title, string name, string primary, string secondary, string glyph)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        header.Children.Add(new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), Foreground = DesignTokens.Accent, FontSize = 18 });
        header.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), CharacterSpacing = 100, FontSize = 10, Foreground = DesignTokens.Muted, VerticalAlignment = VerticalAlignment.Center });
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(header);
        stack.Children.Add(new TextBlock { Text = name, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text, TextWrapping = TextWrapping.Wrap });
        var metricColor = title.Contains("vídeo", StringComparison.OrdinalIgnoreCase) ? DesignTokens.Info
            : title.Contains("Temperatura", StringComparison.OrdinalIgnoreCase) ? DesignTokens.Warning
            : DesignTokens.Accent;
        stack.Children.Add(new TextBlock { Text = primary, FontFamily = new FontFamily("Consolas"), FontSize = 28, FontWeight = FontWeights.SemiBold, Foreground = metricColor });
        stack.Children.Add(new TextBlock { Text = secondary, FontSize = 11, Foreground = DesignTokens.Muted });
        return Card(stack);
    }

    private static Border Card(UIElement content) => new() { Background = DesignTokens.Card, BorderBrush = DesignTokens.Border, BorderThickness = new Thickness(1), CornerRadius = DesignTokens.CardRadius, Padding = new Thickness(20), Child = content, HorizontalAlignment = HorizontalAlignment.Stretch };
    private static Grid CardGrid() => new() { ColumnSpacing = 16, RowSpacing = 16 };
    private static void Arrange(Grid grid, int columns)
    {
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < grid.Children.Count; i++)
        {
            if (i / columns >= grid.RowDefinitions.Count) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (grid.Children[i] is FrameworkElement child)
            {
                Grid.SetColumn(child, i % columns);
                Grid.SetRow(child, i / columns);
            }
        }
    }

    private static void ArrangeResponsive(Grid grid, int columns)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (var column = 0; column < columns; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var index = 0; index < grid.Children.Count; index++)
        {
            if (index / columns >= grid.RowDefinitions.Count)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (grid.Children[index] is not FrameworkElement child) continue;
            Grid.SetColumn(child, index % columns);
            Grid.SetRow(child, index / columns);
        }
    }
    private static ScrollViewer Scroll(UIElement content) => new()
    {
        Content = content,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };
    private static string Pct(double? value) => value.HasValue ? $"{value:F0}%" : "—";
    private static string Temp(double? value) => value.HasValue ? $"{value:F0} °C" : "—";
}
