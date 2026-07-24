using EME.Diagnostics.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace EME.Diagnostics.App.Controls;

public enum TelemetryChartStyle { Line, Area, Step }

public sealed partial class TelemetryChart : Grid
{
    private const int MaximumSamples = 120;
    private readonly Canvas _plot = new() { Height = 230 };
    private readonly Dictionary<string, Series> _series;
    private TelemetryChartStyle _style = TelemetryChartStyle.Line;

    public TelemetryChart(bool isGpu = false, bool isMemory = false, bool isStorage = false)
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowSpacing = 12;

        var legend = new Grid { ColumnSpacing = 18, RowSpacing = 6 };
        for (var column = 0; column < 4; column++)
            legend.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        legend.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        legend.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        List<(string Key, string Label, string Color, int Index)> series;
        if (isStorage)
        {
            series =
            [
                ("usage", "USO", "#4DA3FF", 0),
                ("temperature", "TEMP", "#FF5C6C", 1),
            ];
        }
        else if (isMemory)
        {
            series =
            [
                ("usage", "USO", "#4DA3FF", 0),
                ("temperature", "TEMP", "#FF5C6C", 1),
                ("used", "USADA", "#A970FF", 2),
                ("free", "DISP", "#4CCBA0", 3),
            ];
        }
        else if (isGpu)
        {
            series =
            [
                ("usage", "USO", "#4DA3FF", 0),
                ("temperature", "TEMPERATURA", "#FF5C6C", 1),
                ("clock", "CLOCK", "#A970FF", 2),
                ("power", "POTÊNCIA", "#FFC857", 3)
            ];
            series.Add(("fan", "GPU FAN 1", "#4CCBA0", 4));
            series.Add(("cpuOpt", "GPU FAN 2", "#35C2D8", 5));
            series.Add(("pump", "GPU PUMP", "#FF8A4C", 6));
        }
        else
        {
            series =
            [
                ("usage", "USO", "#4DA3FF", 0),
                ("temperature", "TEMPERATURA", "#FF5C6C", 1),
                ("clock", "CLOCK", "#A970FF", 2),
                ("power", "POTÊNCIA", "#FFC857", 3)
            ];
            if (isGpu)
            {
                series.Add(("fan", "GPU FAN 1", "#4CCBA0", 4));
                series.Add(("cpuOpt", "GPU FAN 2", "#35C2D8", 5));
                series.Add(("pump", "GPU PUMP", "#FF8A4C", 6));
            }
            else
            {
                series.Add(("fan", "CPU FAN", "#4CCBA0", 4));
                series.Add(("cpuOpt", "CPU OPT", "#35C2D8", 5));
                series.Add(("pump", "PUMP", "#FF8A4C", 6));
            }
        }
        _series = new Dictionary<string, Series>(series.Count);
        foreach (var (key, label, color, index) in series)
            _series[key] = CreateSeries(legend, label, color, index);
        foreach (var s in _series.Values) s.Owner = this;
        Children.Add(legend);

        var plotHost = new Border
        {
            Height = 230,
            Background = Brush("#1B1D22"),
            BorderBrush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = _plot
        };
        Grid.SetRow(plotHost, 1);
        Children.Add(plotHost);

        _plot.SizeChanged += (_, _) => Render();
    }

    public void Clear()
    {
        foreach (var series in _series.Values) series.Values.Clear();
        Render();
    }

    public void SetStyle(TelemetryChartStyle style)
    {
        _style = style;
        Render();
    }

    public void SetSeriesVisible(string key, bool visible)
    {
        if (!_series.TryGetValue(key, out var series)) return;
        ApplySeriesVisibility(series, visible);
    }

    public void AddSample(HardwareSnapshot snapshot, bool isGpu = false, bool isMemory = false, bool isStorage = false)
    {
        if (isStorage)
        {
            Add("usage", snapshot.StorageLoad, 100, snapshot.StorageLoad, "%");
            Add("temperature", snapshot.StorageTemperature, 110, snapshot.StorageTemperature, "°C");
        }
        else if (isMemory)
        {
            var total = snapshot.MemoryTotalGb;
            var used = snapshot.MemoryUsedGb;
            var free = total - used;
            var usagePct = total > 0 ? used / total * 100 : 0d;
            Add("usage", usagePct, 100, usagePct, "%");
            Add("temperature", snapshot.MemoryTemperature, 110, snapshot.MemoryTemperature, "°C");
            Add("used", used, total, used, "GB");
            Add("free", free, total, free, "GB");
        }
        else
        {
            var comp = isGpu ? snapshot.Gpu : snapshot.Cpu;
            Add("usage", comp.Usage, 100, comp.Usage, "%");
            Add("temperature", comp.Temperature, 110, comp.Temperature, "°C");
            Add("clock", comp.Clock, 6_000, comp.Clock, "MHz");
            Add("power", comp.Power, 300, comp.Power, "W");
            if (isGpu)
            {
                var gpuFans = snapshot.Fans.Where(fan => !fan.Category.Equals("CPU", StringComparison.OrdinalIgnoreCase)).ToArray();
                var fan1 = gpuFans.ElementAtOrDefault(0);
                var fan2 = gpuFans.ElementAtOrDefault(1);
                var pump = gpuFans.FirstOrDefault(f => f.Name.Contains("pump", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("water", StringComparison.OrdinalIgnoreCase));
                Add("fan", fan1?.Rpm, 5_000, fan1?.Rpm, "RPM");
                Add("cpuOpt", fan2?.Rpm, 5_000, fan2?.Rpm, "RPM");
                Add("pump", pump?.Rpm, 5_000, pump?.Rpm, "RPM");
            }
            else
            {
                var cpuFans = snapshot.Fans.Where(fan => fan.Category.Equals("CPU", StringComparison.OrdinalIgnoreCase)).ToArray();
                var fan = FindFan(cpuFans, "CPU Fan", "CPU_FAN");
                var optional = FindFan(cpuFans, "CPU Optional", "CPU OPT", "CPU_OPT");
                var pump = FindFan(cpuFans, "Pump", "AIO", "Water");
                Add("fan", fan?.Rpm, 5_000, fan?.Rpm, "RPM");
                Add("cpuOpt", optional?.Rpm, 5_000, optional?.Rpm, "RPM");
                Add("pump", pump?.Rpm, 5_000, pump?.Rpm, "RPM");
            }
        }
        Render();
    }

    private static FanMetric? FindFan(IEnumerable<FanMetric> fans, params string[] aliases) =>
        fans.FirstOrDefault(fan => aliases.Any(alias => fan.Name.Contains(alias, StringComparison.OrdinalIgnoreCase)));

    private void Add(string key, double? value, double maximum, double? displayValue, string unit)
    {
        var series = _series[key];
        series.Values.Add(value.HasValue ? Math.Clamp(value.Value / maximum * 100d, 0d, 100d) : null);
        if (series.Values.Count > MaximumSamples) series.Values.RemoveAt(0);
        series.Value.Text = displayValue.HasValue ? $"{series.Label}: {displayValue.Value:0.#} {unit}" : $"{series.Label}: —";
    }

    private void Render()
    {
        var width = _plot.ActualWidth;
        var height = _plot.ActualHeight;
        if (width <= 0 || height <= 0) return;

        _plot.Children.Clear();
        for (var line = 1; line < 4; line++)
        {
            var y = height * line / 4d;
            _plot.Children.Add(new Line
            {
                X1 = 0,
                X2 = width,
                Y1 = y,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
                StrokeThickness = 1
            });
        }

        foreach (var series in _series.Values)
        {
            if (!series.Visible || series.Values.All(value => !value.HasValue)) continue;
            var divisor = Math.Max(MaximumSamples - 1, 1);
            var points = new List<Point>();
            Point? lastPoint = null;
            for (var index = 0; index < series.Values.Count; index++)
            {
                if (!series.Values[index].HasValue) continue;
                var x = width * index / divisor;
                var y = height - height * series.Values[index]!.Value / 100d;
                lastPoint = new Point(x, y);
                if (_style == TelemetryChartStyle.Step && points.Count > 0)
                    points.Add(new Point(x, points[^1].Y));
                points.Add(lastPoint.Value);
            }

            if (_style == TelemetryChartStyle.Area && points.Count > 0)
            {
                var area = new Polygon
                {
                    Fill = new SolidColorBrush(series.Color.Color) { Opacity = 0.16 }
                };
                area.Points.Add(new Point(points[0].X, height));
                foreach (var point in points) area.Points.Add(point);
                area.Points.Add(new Point(points[^1].X, height));
                _plot.Children.Add(area);
            }

            var polyline = new Polyline { Stroke = series.Color, StrokeThickness = _style == TelemetryChartStyle.Area ? 2.5 : 2 };
            foreach (var point in points) polyline.Points.Add(point);
            _plot.Children.Add(polyline);
            if (lastPoint.HasValue)
            {
                var marker = new Ellipse
                {
                    Width = 9,
                    Height = 9,
                    Fill = series.Color
                };
                Canvas.SetLeft(marker, lastPoint.Value.X - marker.Width / 2);
                Canvas.SetTop(marker, lastPoint.Value.Y - marker.Height / 2);
                _plot.Children.Add(marker);
            }
        }
    }

    private static Series CreateSeries(Grid legend, string label, string color, int index)
    {
        var brush = Brush(color);
        var value = new TextBlock { Text = $"{label}: —", Foreground = brush, FontSize = 11, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0) };
        var checkGlyph = new FontIcon
        {
            Glyph = "\uE73E",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 8,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 10, 11, 13))
        };
        var checkSurface = new Border
        {
            Width = 14,
            Height = 14,
            Background = brush,
            BorderBrush = brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 0, 2, 0),
            Child = checkGlyph
        };
        var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, Margin = new Thickness(0) };
        item.Children.Add(checkSurface);
        item.Children.Add(value);
        Grid.SetColumn(item, index % 4);
        Grid.SetRow(item, index / 4);
        legend.Children.Add(item);

        var series = new Series(label, brush, value, checkSurface, checkGlyph, new List<double?>());
        checkSurface.Tapped += (_, _) => series.Owner?.ApplySeriesVisibility(series, !series.Visible);
        return series;
    }

    private static SolidColorBrush Brush(string hex)
    {
        var value = hex.TrimStart('#');
        return new SolidColorBrush(Color.FromArgb(255,
            Convert.ToByte(value[..2], 16),
            Convert.ToByte(value.Substring(2, 2), 16),
            Convert.ToByte(value.Substring(4, 2), 16)));
    }

    private void ApplySeriesVisibility(Series series, bool visible)
    {
        series.Visible = visible;
        series.Value.Opacity = visible ? 1 : 0.35;
        series.Toggle.Background = visible ? series.Color : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        series.Glyph.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        Render();
    }

    private sealed class Series(string label, SolidColorBrush color, TextBlock value, Border toggle, FontIcon glyph, List<double?> values)
    {
        public string Label { get; } = label;
        public SolidColorBrush Color { get; } = color;
        public TextBlock Value { get; } = value;
        public Border Toggle { get; } = toggle;
        public FontIcon Glyph { get; } = glyph;
        public List<double?> Values { get; } = values;
        public bool Visible { get; set; } = true;
        public TelemetryChart? Owner { get; set; }
    }
}
