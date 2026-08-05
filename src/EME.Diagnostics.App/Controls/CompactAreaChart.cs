using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using EME.Diagnostics.App.Theme;
using Windows.Foundation;
using Windows.UI;

namespace EME.Diagnostics.App.Controls;

public sealed partial class CompactAreaChart : Grid
{
    private const int MaximumSamples = 60;
    private readonly Canvas _plot = new() { Height = 154 };
    private readonly List<double> _values = [];
    private readonly SolidColorBrush _color;
    private bool _isLoaded;

    public CompactAreaChart(string title, string color, double height = 222)
    {
        _color = Brush(color);
        Height = height;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            CharacterSpacing = 160,
            Foreground = DesignTokens.Muted
        });
        var range = new TextBlock { Text = "Últimos 60 segundos", FontSize = 11, Foreground = DesignTokens.Muted };
        Grid.SetColumn(range, 1);
        header.Children.Add(range);
        Children.Add(header);

        Grid.SetRow(_plot, 1);
        Children.Add(_plot);
        Loaded += (_, _) =>
        {
            _isLoaded = true;
            Render();
        };
        Unloaded += (_, _) => _isLoaded = false;
        _plot.SizeChanged += (_, _) =>
        {
            if (_isLoaded) Render();
        };
    }

    public void AddSample(double? value)
    {
        _values.Add(Math.Clamp(value ?? 0, 0, 100));
        if (_values.Count > MaximumSamples) _values.RemoveAt(0);
        if (_isLoaded) Render();
    }

    private void Render()
    {
        var width = _plot.ActualWidth;
        var height = _plot.ActualHeight;
        if (!_isLoaded || XamlRoot is null || width <= 0 || height <= 0) return;
        _plot.Children.Clear();

        for (var line = 0; line <= 4; line++)
        {
            var y = height * line / 4d;
            _plot.Children.Add(new Line
            {
                X1 = 0, X2 = width, Y1 = y, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                StrokeThickness = 1
            });
        }

        if (_values.Count == 0) return;
        var points = new List<Point>(_values.Count);
        for (var index = 0; index < _values.Count; index++)
        {
            var x = width * index / Math.Max(MaximumSamples - 1, 1);
            var y = height - (height * _values[index] / 100d);
            points.Add(new Point(x, y));
        }

        var gradient = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        gradient.GradientStops.Add(new GradientStop { Color = _color.Color, Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, _color.Color.R, _color.Color.G, _color.Color.B), Offset = 1 });
        var area = new Polygon { Fill = gradient, Opacity = 0.32 };
        area.Points.Add(new Point(points[0].X, height));
        foreach (var point in points) area.Points.Add(point);
        area.Points.Add(new Point(points[^1].X, height));
        _plot.Children.Add(area);

        var linePath = new Polyline { Stroke = _color, StrokeThickness = 2.2 };
        foreach (var point in points) linePath.Points.Add(point);
        _plot.Children.Add(linePath);
    }

    private static SolidColorBrush Brush(string hex)
    {
        var value = hex.TrimStart('#');
        return new SolidColorBrush(Color.FromArgb(255,
            Convert.ToByte(value[..2], 16), Convert.ToByte(value.Substring(2, 2), 16), Convert.ToByte(value.Substring(4, 2), 16)));
    }
}
