using System.Linq;
using System.Threading;
using EME.Diagnostics.App.Theme;
using EME.Diagnostics.App.Controls;
using EME.Diagnostics.App.ViewModels;
using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace EME.Diagnostics.App;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly StressCatalogService _stressCatalog;
    private readonly ContentControl _content = new();
    private readonly TextBlock _status = new();
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
    private Button? _storageStressStart;
    private Button? _storageStressStop;
    private DateTimeOffset _lastCpuChartSample = DateTimeOffset.MinValue;
    private DateTimeOffset _lastGpuChartSample = DateTimeOffset.MinValue;
    private DateTimeOffset _lastMemoryChartSample = DateTimeOffset.MinValue;
    private DateTimeOffset _lastStorageChartSample = DateTimeOffset.MinValue;

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
        if (e.PropertyName == nameof(MainViewModel.Snapshot) && _viewModel.CurrentPage == "Stress Test")
        {
            _cpuTelemetryChart?.AddSample(_viewModel.Snapshot);
            _gpuTelemetryChart?.AddSample(_viewModel.Snapshot, isGpu: true);
            _memoryTelemetryChart?.AddSample(_viewModel.Snapshot, isMemory: true);
            _storageTelemetryChart?.AddSample(_viewModel.Snapshot, isStorage: true);
        }
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
            });
        };
        Activated += async (_, _) => { if (_viewModel.Snapshot == HardwareSnapshot.Empty) await _viewModel.StartAsync(); };
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void BuildShell()
    {
        Root.Background = DesignTokens.Background;
        _content.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _content.VerticalContentAlignment = VerticalAlignment.Stretch;
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(224) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Root.Children.Add(BuildSidebar());

        var host = new Grid { Padding = new Thickness(32, 24, 32, 20) };
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
        var panel = new StackPanel { Spacing = 8, Padding = new Thickness(16, 22, 16, 16) };
        panel.Children.Add(new TextBlock { Text = "E.M.E", FontSize = 11, CharacterSpacing = 220, Foreground = DesignTokens.Accent, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = "Diagnostics", FontSize = 21, Foreground = DesignTokens.Text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, -6, 0, 24) });
        foreach (var item in new[] { ("Dashboard", "\uE80F"), ("Stress Test", "\uE945"), ("Benchmark", "\uE9D9"), ("Hardware", "\uE950"), ("Relatórios", "\uE9F9"), ("Configurações", "\uE713") })
            panel.Children.Add(NavButton(item.Item1, item.Item2));
        panel.Children.Add(new Border { Height = 1, Background = DesignTokens.Border, Margin = new Thickness(8, 16, 8, 8) });
        panel.Children.Add(new TextBlock { Text = "v0.1.0  •  Estrutura inicial", FontFamily = new FontFamily("Consolas"), FontSize = 10, Foreground = DesignTokens.Muted, Margin = new Thickness(8, 8, 0, 0) });
        return new Border { Background = DesignTokens.Sidebar, BorderBrush = DesignTokens.Border, BorderThickness = new Thickness(0, 0, 1, 0), Child = panel };
    }

    private Button NavButton(string label, string glyph)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16 });
        row.Children.Add(new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = row, Tag = label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), Foreground = DesignTokens.Text, BorderThickness = new Thickness(0) };
        button.Click += (_, _) => _viewModel.NavigateCommand.Execute(label);
        return button;
    }

    private void ShowPage()
    {
        _content.Content = _viewModel.CurrentPage switch
        {
            "Dashboard" => Dashboard(),
            "Stress Test" => StressTest(),
            "Benchmark" => Placeholder("Benchmark", "A interface está preparada para suítes de benchmark futuras."),
            "Hardware" => Hardware(),
            "Relatórios" => Placeholder("Relatórios", "Aqui ficarão histórico, filtros e exportação PDF de hardware, resultados, temperaturas, duração e conclusão."),
            "Configurações" => Placeholder("Configurações", "Preferências de atualização, limites térmicos, tema e comportamento dos testes."),
            _ => Dashboard()
        };
        _status.Text = _viewModel.Status;
    }

    private UIElement Dashboard()
    {
        var s = _viewModel.Snapshot;
        _dashboardStructureSignature = GetStructureSignature(s);
        var page = Page("Dashboard", $"{s.Devices.Count} componentes detectados • todos os sensores expostos pelo LibreHardwareMonitor.");

        if (s.Devices.Count == 0)
        {
            page.Children.Add(Card(new TextBlock
            {
                Text = "Nenhum hardware foi retornado. Alguns sensores exigem execução como administrador.",
                Foreground = DesignTokens.Muted,
                TextWrapping = TextWrapping.Wrap
            }));
        }
        else
        {
            foreach (var device in s.Devices)
                page.Children.Add(HardwareListCard(device));
        }
        return Scroll(page);
    }

    private Border HardwareListCard(HardwareDeviceSnapshot device)
    {
        var stack = new StackPanel { Spacing = 14 };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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
        header.Children.Add(titleStack);

        var badge = new Border
        {
            Background = DesignTokens.Inset,
            BorderBrush = DesignTokens.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 5, 10, 5),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = device.Type, FontSize = 10, Foreground = DesignTokens.Accent, CharacterSpacing = 70 }
        };
        Grid.SetColumn(badge, 1);
        header.Children.Add(badge);
        stack.Children.Add(header);

        if (device.Sensors.Count == 0)
        {
            stack.Children.Add(new TextBlock { Text = "Nenhum sensor dinâmico exposto por este componente.", Foreground = DesignTokens.Muted, FontStyle = Windows.UI.Text.FontStyle.Italic });
        }
        else
        {
            var sensorGrid = new Grid { RowSpacing = 0 };
            sensorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.7, GridUnitType.Star) });
            sensorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sensorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            sensorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            sensorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            AddSensorRow(sensorGrid, 0, "SENSOR", "TIPO", "ATUAL", "MÍNIMO", "MÁXIMO", true);
            for (var index = 0; index < device.Sensors.Count; index++)
            {
                AddSensorDataRow(sensorGrid, index + 1, device.Sensors[index]);
            }
            stack.Children.Add(sensorGrid);
        }

        return Card(stack);
    }

    private void AddSensorDataRow(Grid grid, int row, SensorMetric sensor)
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
        }
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

    private UIElement StressTest()
    {
        var page = Page("Stress Test", "Execute cargas controladas e acompanhe o progresso em tempo real.");
        foreach (var test in _stressCatalog.GetDefinitions())
        {
            if (test.Target == StressTarget.Cpu)
            {
                page.Children.Add(CpuStressCard(test));
                continue;
            }
            if (test.Target == StressTarget.Gpu)
            {
                page.Children.Add(GpuStressCard(test));
                continue;
            }
            if (test.Target == StressTarget.Memory)
            {
                page.Children.Add(MemoryStressCard(test));
                continue;
            }
            if (test.Target == StressTarget.Storage)
            {
                page.Children.Add(StorageStressCard(test));
                continue;
            }

            var stack = new StackPanel { Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = test.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });
            stack.Children.Add(new TextBlock { Text = test.Description, TextWrapping = TextWrapping.Wrap, Foreground = DesignTokens.Muted });
            stack.Children.Add(new TextBlock { Text = $"Duração padrão: {test.DefaultDuration.TotalMinutes:F0} min", FontFamily = new FontFamily("Consolas"), FontSize = 11, Foreground = DesignTokens.Accent });
            var pending = new Button { Content = "Ainda não implementado", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left };
            stack.Children.Add(pending);
            page.Children.Add(Card(stack));
        }
        return Scroll(page);
    }

    private Border CpuStressCard(StressTestDefinition test)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock { Text = test.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });
        stack.Children.Add(new TextBlock { Text = test.Description, TextWrapping = TextWrapping.Wrap, Foreground = DesignTokens.Muted, MinHeight = 42 });

        stack.Children.Add(new TextBlock
        {
            Text = "Duração inicial: 1 minuto",
            Foreground = DesignTokens.Accent,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11
        });

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
            await _viewModel.StartCpuStressAsync(TimeSpan.FromMinutes(1));
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
        _cpuStressStart.IsEnabled = !running;
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
            await _viewModel.StartGpuStressAsync(TimeSpan.Zero);
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

        // Duration selector
        var durationRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 4) };
        var durationCombo = new ComboBox { SelectedIndex = 2, MinWidth = 140 };
        var durationItems = new (string Label, TimeSpan Value)[]
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
        foreach (var (label, val) in durationItems)
            durationCombo.Items.Add(new ComboBoxItem { Content = label, Tag = val });
        var customMinutesBox = new TextBox
        {
            PlaceholderText = "min",
            Width = 80,
            Visibility = Visibility.Collapsed
        };
        durationCombo.SelectionChanged += (_, _) =>
        {
            customMinutesBox.Visibility = durationCombo.SelectedIndex == 7 ? Visibility.Visible : Visibility.Collapsed;
        };
        durationRow.Children.Add(durationCombo);
        durationRow.Children.Add(customMinutesBox);
        stack.Children.Add(new TextBlock { Text = "DURAÇÃO", CharacterSpacing = 100, FontSize = 9, Foreground = DesignTokens.Muted });
        stack.Children.Add(durationRow);

        var currentDuration = TimeSpan.FromMinutes(2);

        _memoryStressState.Text = "Pronto para testar RAM";
        _memoryStressMetrics.Text = $"Padrões • {Environment.ProcessorCount} threads • 100% da RAM disponível";

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _memoryStressStart = new Button { Content = "Iniciar teste de RAM", HorizontalAlignment = HorizontalAlignment.Left };
        _memoryStressStop = new Button { Content = "Parar", HorizontalAlignment = HorizontalAlignment.Left };
        _memoryStressStart.Click += async (_, _) =>
        {
            var selected = (ComboBoxItem)durationCombo.SelectedItem;
            var value = (TimeSpan)selected.Tag;
            if (value == TimeSpan.Zero)
            {
                if (!int.TryParse(customMinutesBox.Text, out var mins) || mins < 1)
                {
                    return;
                }
                value = TimeSpan.FromMinutes(mins);
            }
            currentDuration = value;
            await _viewModel.StartMemoryStressAsync(currentDuration);
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
        _memoryStressStart.IsEnabled = !running && !cancelling;
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

        _storageStressState.Text = "Pronto para testar Storage";
        _storageStressMetrics.Text = "Arquivo temporário de 1 GB • lotes de 64 KB";

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _storageStressStart = new Button { Content = "Iniciar teste de Storage", HorizontalAlignment = HorizontalAlignment.Left };
        _storageStressStop = new Button { Content = "Parar", HorizontalAlignment = HorizontalAlignment.Left };
        _storageStressStart.Click += async (_, _) =>
        {
            await _viewModel.StartStorageStressAsync(test.DefaultDuration);
        };
        _storageStressStop.Click += (_, _) => _viewModel.StopStorageStress();
        actions.Children.Add(_storageStressStart);
        actions.Children.Add(_storageStressStop);
        stack.Children.Add(actions);
        UpdateStorageStressUi();
        return Card(stack);
    }

    private void UpdateStorageStressUi()
    {
        if (_storageStressState is null || _storageStressMetrics is null || _storageStressStart is null || _storageStressStop is null) return;

        var running = _viewModel.StorageStressStatus == StressStatus.Running;
        var cancelling = _viewModel.StorageStressStatus == StressStatus.Cancelling;
        var metrics = _viewModel.StorageStressMetrics;
        _storageStressState.Text = _viewModel.StorageStressStatus switch
        {
            StressStatus.Running => "Estressando Storage...",
            StressStatus.Cancelling => "Cancelando...",
            StressStatus.Completed => "Storage OK — sem erros",
            StressStatus.Cancelled => "Teste de Storage cancelado",
            StressStatus.Failed => $"Falha! {metrics?.Errors ?? 0} erro(s)",
            _ => "Pronto para testar Storage"
        };
        _storageStressMetrics.Text = metrics is null
            ? "Aguardando início"
            : $"{metrics.Elapsed:mm\\:ss}  •  {metrics.ProgressPercent:0.0}%  •  " +
              $"{metrics.ThroughputMBs:0.0} MB/s  •  {metrics.Operations} ops  •  {metrics.Errors} erro(s)";
        _storageStressStart.IsEnabled = !running && !cancelling;
        _storageStressStop.IsEnabled = running || cancelling;
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
        _gpuStressStart.IsEnabled = !running && !cancelling && _viewModel.IsGpuStressAvailable;
        _gpuStressStop.IsEnabled = running || cancelling;
    }

    private UIElement Hardware()
    {
        var s = _viewModel.Snapshot;
        var page = Page("Hardware", "Sensores brutos normalizados para diagnóstico.");
        page.Children.Add(MetricCard("Processador", s.Cpu.Name, $"Uso {Pct(s.Cpu.Usage)}", $"Temperatura {Temp(s.Cpu.Temperature)}", "\uE950"));
        page.Children.Add(MetricCard("Placa de vídeo", s.Gpu.Name, $"Uso {Pct(s.Gpu.Usage)}", $"Temperatura {Temp(s.Gpu.Temperature)}", "\uE7F4"));
        foreach (var fan in s.Fans) page.Children.Add(MetricCard("Ventoinha", fan.Name, $"{fan.Rpm:F0} RPM", "Leitura em tempo real", "\uE9CA"));
        return Scroll(page);
    }

    private static StackPanel Page(string title, string subtitle)
    {
        var stack = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Stretch };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 30, FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text });
        stack.Children.Add(new TextBlock { Text = subtitle, FontSize = 13, Foreground = DesignTokens.Muted, Margin = new Thickness(0, -10, 0, 8) });
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
        stack.Children.Add(new TextBlock { Text = primary, FontFamily = new FontFamily("Consolas"), FontSize = 25, Foreground = DesignTokens.Accent });
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
