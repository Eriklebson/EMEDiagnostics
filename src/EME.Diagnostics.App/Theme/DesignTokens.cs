using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace EME.Diagnostics.App.Theme;

public static class DesignTokens
{
    public static readonly SolidColorBrush Background = Brush("#0D0F10");
    public static readonly SolidColorBrush Sidebar = Brush("#080A0B");
    public static readonly SolidColorBrush Card = Brush("#17191A");
    public static readonly SolidColorBrush Inset = Brush("#111314");
    public static readonly SolidColorBrush Text = Brush("#F1F2F2");
    public static readonly SolidColorBrush Muted = Brush("#8B9093");
    public static readonly SolidColorBrush Accent = Brush("#42D286");
    public static readonly SolidColorBrush AccentBright = Brush("#6EE7A5");
    public static readonly SolidColorBrush AccentSubtle = new(Color.FromArgb(38, 66, 210, 134));
    public static readonly SolidColorBrush DangerSubtle = new(Color.FromArgb(38, 232, 77, 77));
    public static readonly SolidColorBrush WarningSubtle = new(Color.FromArgb(38, 255, 178, 28));
    public static readonly SolidColorBrush Info = Brush("#43A8E5");
    public static readonly SolidColorBrush Warning = Brush("#FFB21C");
    public static readonly SolidColorBrush Danger = Brush("#E84D4D");
    public static readonly SolidColorBrush Border = Brush("#2A2D2F");
    public static readonly SolidColorBrush NavSelected = Brush("#1C1E20");
    public static readonly CornerRadius CardRadius = new(10);

    private static SolidColorBrush Brush(string hex)
    {
        var value = hex.TrimStart('#');
        return new SolidColorBrush(Color.FromArgb(255,
            Convert.ToByte(value[..2], 16), Convert.ToByte(value.Substring(2, 2), 16), Convert.ToByte(value.Substring(4, 2), 16)));
    }
}
