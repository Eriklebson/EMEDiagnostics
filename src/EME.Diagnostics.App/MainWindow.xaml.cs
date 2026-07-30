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
                if (e.PropertyName is nameof(MainViewModel.ReceivedReports) or nameof(MainViewModel.ConnectedClients) or nameof(MainViewModel.IsServerMode) or nameof(MainViewModel.IsClientConnected)
                    && _viewModel.CurrentPage == "Rede") ShowPage();
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
        foreach (var item in new[] { ("Dashboard", "\uE80F"), ("Stress Test", "\uE945"), ("Hardware", "\uE950"), ("Relatórios", "\uE9F9"), ("Rede", "\uE8CE"), ("Configurações", "\uE713") })
            panel.Children.Add(NavButton(item.Item1, item.Item2));
        panel.Children.Add(new Border { Height = 1, Background = DesignTokens.Border, Margin = new Thickness(8, 16, 8, 8) });
        panel.Children.Add(new TextBlock { Text = $"v{ProductInfo.Version}  •  Release", FontFamily = new FontFamily("Consolas"), FontSize = 10, Foreground = DesignTokens.Muted, Margin = new Thickness(8, 8, 0, 0) });
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

    private async void ShowPage()
    {
        _content.Content = _viewModel.CurrentPage switch
        {
            "Dashboard" => Dashboard(),
            "Stress Test" => StressTest(),
            "Hardware" => Hardware(),
            "Relatórios" => await ReportsPageAsync(),
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
            ShowPage();
    }

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
                foreach (var r in _viewModel.ReceivedReports)
                {
                    var reportCard = new Border
                    {
                        Background = DesignTokens.Card,
                        BorderBrush = DesignTokens.Border,
                        BorderThickness = new Thickness(1),
                        CornerRadius = DesignTokens.CardRadius,
                        Padding = new Thickness(14),
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    var row = new StackPanel { Spacing = 4 };
                    row.Children.Add(new TextBlock { Text = $"{r.MachineName}  •  {r.TestType}  •  {r.CreatedAt:dd/MM/yyyy HH:mm}", FontWeight = FontWeights.SemiBold, Foreground = DesignTokens.Text, FontSize = 13 });
                    row.Children.Add(new TextBlock { Text = $"Duração: {r.Duration}  •  Status: {r.Status}  •  Resultado: {r.Result}  •  Tamanho: {r.PdfSizeBytes / 1024} KB", FontSize = 10, Foreground = DesignTokens.Muted, FontFamily = new FontFamily("Consolas") });
                    reportCard.Child = row;
                    contentStack.Children.Add(reportCard);
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
        var page = Page("Hardware", "Sensores brutos normalizados para diagnóstico.");
        page.Children.Add(MetricCard("Processador", s.Cpu.Name, $"Uso {Pct(s.Cpu.Usage)}", $"Temperatura {Temp(s.Cpu.Temperature)}", "\uE950"));
        page.Children.Add(MetricCard("Placa de vídeo", s.Gpu.Name, $"Uso {Pct(s.Gpu.Usage)}", $"Temperatura {Temp(s.Gpu.Temperature)}", "\uE7F4"));
        var ramUsage = s.MemoryTotalGb > 0 ? $"{(s.MemoryUsedGb / s.MemoryTotalGb * 100):F1}%" : "—";
        var ramTemp = s.MemoryTemperature.HasValue ? $"Temperatura {s.MemoryTemperature:F0}°C" : "Sem sensor térmico";
        page.Children.Add(MetricCard("Memória RAM", $"{s.MemoryTotalGb:F1} GB total", $"{s.MemoryUsedGb:F1} GB usados ({ramUsage})", ramTemp, "\uE93B"));
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
