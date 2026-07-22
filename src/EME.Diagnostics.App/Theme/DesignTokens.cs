using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace EME.Diagnostics.App.Theme;

public static class DesignTokens
{
    public static readonly SolidColorBrush Background = Brush("#0A0B0D");
    public static readonly SolidColorBrush Sidebar = Brush("#161719");
    public static readonly SolidColorBrush Card = Brush("#2A2D31");
    public static readonly SolidColorBrush Inset = Brush("#1B1D22");
    public static readonly SolidColorBrush Text = Brush("#E8E9EB");
    public static readonly SolidColorBrush Muted = Brush("#A8ABB0");
    public static readonly SolidColorBrush Accent = Brush("#4CCBA0");
    public static readonly SolidColorBrush Warning = Brush("#E6A030");
    public static readonly SolidColorBrush Danger = Brush("#E84D4D");
    public static readonly SolidColorBrush Border = new(Color.FromArgb(28, 255, 255, 255));
    public static readonly CornerRadius CardRadius = new(12);

    private static SolidColorBrush Brush(string hex)
    {
        var value = hex.TrimStart('#');
        return new SolidColorBrush(Color.FromArgb(255,
            Convert.ToByte(value[..2], 16), Convert.ToByte(value.Substring(2, 2), 16), Convert.ToByte(value.Substring(4, 2), 16)));
    }
}
