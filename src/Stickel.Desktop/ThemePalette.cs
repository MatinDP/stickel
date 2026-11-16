using System.Windows;
using System.Windows.Media;

namespace Stickel.Desktop;

internal static class ThemePalette
{
    internal static void Apply(bool light)
    {
        var keys = new[] { "Bg", "Panel", "Raised", "Line", "Text", "Muted", "Accent", "AccentInk", "Well", "SoftEdge", "HoverSurface", "SelectedSurface", "Popup" };
        var colors = light
            ? new[] { "#FFFFFF", "#FFFFFF", "#F7F7F7", "#BDBDBD", "#161616", "#5F5F5F", "#171717", "#FFFFFF", "#FAFAFA", "#DEDEDE", "#EDEDED", "#E5E5E5", "#FFFFFF" }
            : new[] { "#000000", "#101010", "#191919", "#3B3B3B", "#F2F2F2", "#ADADAD", "#F5F5F5", "#111111", "#080808", "#292929", "#242424", "#2A2A2A", "#191919" };
        for (var i = 0; i < keys.Length; i++) Set(keys[i], Solid(colors[i]));
        if (SystemParameters.HighContrast)
        {
            foreach (var key in new[] { "Bg", "Panel", "Raised", "Well", "Popup", "HoverSurface", "SelectedSurface" }) Set(key, SystemColors.WindowBrush);
            foreach (var key in new[] { "Text", "Muted", "Line", "SoftEdge" }) Set(key, SystemColors.WindowTextBrush);
            Set("Accent", SystemColors.HighlightBrush); Set("AccentInk", SystemColors.HighlightTextBrush);
        }
    }
    private static void Set(string key, Brush brush) => Application.Current.Resources[key] = brush;
    private static Color Parse(string color) => (Color)ColorConverter.ConvertFromString(color);
    private static SolidColorBrush Solid(string color) { var brush = new SolidColorBrush(Parse(color)); brush.Freeze(); return brush; }
}
