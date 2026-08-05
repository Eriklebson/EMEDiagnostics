using EME.Diagnostics.App.Theme;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace EME.Diagnostics.App.Controls;

public sealed record MultiLineSeries(string Key, string Label, string Color, string Unit, double? Maximum = 100);

public sealed partial class MultiLineChart : Grid
{
    private const int MaximumSamples = 60;
    private readonly Canvas _plot = new() { Height = 132 };
    private readonly List<SeriesState> _series = [];
    private bool _isLoaded;

    public MultiLineChart(string title, IReadOnlyList<MultiLineSeries> series, double height = 222, bool showValues = true)
    {
        Height = height;
        foreach (var definition in series)
            _series.Add(new SeriesState(definition, HexBrush(definition.Color)));

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), FontFamily = new FontFamily("Consolas"), FontSize = 10, CharacterSpacing = 160, Foreground = DesignTokens.Muted });
        var range = new TextBlock { Text = "Últimos 60 segundos", FontSize = 11, Foreground = DesignTokens.Muted };
        Grid.SetColumn(range, 1);
        header.Children.Add(range);
        Children.Add(header);

        var legend = new Grid { ColumnSpacing = 14, RowSpacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        for (var index = 0; index < _series.Count; index++) legend.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        legend.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var index = 0; index < _series.Count; index++)
        {
            var state = _series[index];
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            item.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(2), Background = state.Color, VerticalAlignment = VerticalAlignment.Center });
            item.Children.Add(new TextBlock { Text = state.Definition.Label, FontFamily = new FontFamily("Consolas"), FontSize = 10, Foreground = DesignTokens.Muted });
            if (showValues)
            {
                state.ValueText = new TextBlock { Text = "—", FontFamily = new FontFamily("Consolas"), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = state.Color };
                item.Children.Add(state.ValueText);
            }
            Grid.SetColumn(item, index);
            legend.Children.Add(item);
        }
        Grid.SetRow(legend, 1);
        Children.Add(legend);

        Grid.SetRow(_plot, 2);
        Children.Add(_plot);
        Loaded += (_, _) => { _isLoaded = true; Render(); };
        Unloaded += (_, _) => _isLoaded = false;
        _plot.SizeChanged += (_, _) => { if (_isLoaded) Render(); };
    }

    public void AddSamples(params (string Key, double? Value)[] samples)
    {
        foreach (var state in _series)
        {
            var sample = samples.FirstOrDefault(item => item.Key.Equals(state.Definition.Key, StringComparison.OrdinalIgnoreCase));
            state.Values.Add(Math.Max(0, sample.Value ?? 0));
            if (state.Values.Count > MaximumSamples) state.Values.RemoveAt(0);
            if (state.ValueText != null)
                state.ValueText.Text = sample.Value.HasValue ? $"{sample.Value.Value:F0}{state.Definition.Unit}" : "—";
        }
        if (_isLoaded) Render();
    }

    private void Render()
    {
        var width = _plot.ActualWidth;
        var height = _plot.ActualHeight;
        if (!_isLoaded || XamlRoot is null || width <= 0 || height <= 0) return;
        _plot.Children.Clear();

        for (var index = 0; index <= 4; index++)
        {
            var y = height * index / 4d;
            _plot.Children.Add(new Line { X1 = 0, X2 = width, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), StrokeThickness = 1 });
        }

        foreach (var state in _series)
        {
            if (state.Values.Count == 0) continue;
            var maximum = state.Definition.Maximum ?? Math.Max(10, state.Values.Max() * 1.15);
            var points = new List<Point>(state.Values.Count);
            for (var index = 0; index < state.Values.Count; index++)
            {
                var x = width * index / Math.Max(MaximumSamples - 1, 1);
                var normalized = Math.Clamp(state.Values[index] / maximum, 0, 1);
                points.Add(new Point(x, height - height * normalized));
            }

            var gradient = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
            gradient.GradientStops.Add(new GradientStop { Color = state.Color.Color, Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, state.Color.Color.R, state.Color.Color.G, state.Color.Color.B), Offset = 1 });
            var area = new Polygon { Fill = gradient, Opacity = 0.18 };
            area.Points.Add(new Point(points[0].X, height));
            foreach (var point in points) area.Points.Add(point);
            area.Points.Add(new Point(points[^1].X, height));
            _plot.Children.Add(area);

            var line = new Polyline { Stroke = state.Color, StrokeThickness = 2.2 };
            foreach (var point in points) line.Points.Add(point);
            _plot.Children.Add(line);
        }
    }

    private static SolidColorBrush HexBrush(string hex)
    {
        var value = hex.TrimStart('#');
        return new SolidColorBrush(Color.FromArgb(255, Convert.ToByte(value[..2], 16), Convert.ToByte(value.Substring(2, 2), 16), Convert.ToByte(value.Substring(4, 2), 16)));
    }

    private sealed class SeriesState(MultiLineSeries definition, SolidColorBrush color)
    {
        public MultiLineSeries Definition { get; } = definition;
        public SolidColorBrush Color { get; } = color;
        public List<double> Values { get; } = [];
        public TextBlock? ValueText { get; set; }
    }
}
