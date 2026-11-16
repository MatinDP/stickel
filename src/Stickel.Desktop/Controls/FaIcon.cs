using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Stickel.Desktop.Controls;

/// <summary>Selected Font Awesome SVG paths, embedded with the app for offline rendering.</summary>
public sealed class FaIcon : Control
{
    private sealed record IconData(double Width, double Height, string Path);
    private static readonly IReadOnlyDictionary<string, (double Width, double Height, Geometry Geometry)> Icons = LoadIcons();
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(FaIcon), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    public FaIcon()
    {
        Width = Height = 16; Focusable = false; IsHitTestVisible = false; FlowDirection = FlowDirection.LeftToRight;
        VerticalAlignment = VerticalAlignment.Center;
    }

    private static IReadOnlyDictionary<string, (double Width, double Height, Geometry Geometry)> LoadIcons()
    {
        using var stream = typeof(FaIcon).Assembly.GetManifestResourceStream("Stickel.Desktop.Assets.FontAwesome.icons.json") ?? throw new InvalidDataException("Font Awesome icon resources are missing.");
        var data = JsonSerializer.Deserialize<Dictionary<string, IconData>>(stream)!;
        return data.ToDictionary(x => x.Key, x =>
        {
            var geometry = Geometry.Parse("F1 " + x.Value.Path); geometry.Freeze();
            return (x.Value.Width, x.Value.Height, geometry);
        });
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (!Icons.TryGetValue(Kind, out var icon) || ActualWidth <= 0 || ActualHeight <= 0) return;
        var scale = Math.Min(ActualWidth / icon.Width, ActualHeight / icon.Height);
        drawingContext.PushTransform(new MatrixTransform(scale, 0, 0, scale, (ActualWidth - icon.Width * scale) / 2, (ActualHeight - icon.Height * scale) / 2));
        drawingContext.DrawGeometry(Foreground, null, icon.Geometry);
        drawingContext.Pop();
    }
}

public static class IconAssist
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.RegisterAttached("Kind", typeof(string), typeof(IconAssist), new PropertyMetadata(""));
    public static string GetKind(DependencyObject target) => (string)target.GetValue(KindProperty);
    public static void SetKind(DependencyObject target, string value) => target.SetValue(KindProperty, value);
}
