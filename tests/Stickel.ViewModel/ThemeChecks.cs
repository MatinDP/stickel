using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Stickel.Desktop;
using Stickel.Desktop.Controls;
using Localization = Stickel.Desktop.Localization;

namespace Stickel.ViewModelTests;

internal static class ThemeChecks
{
    private static int checks;
    internal static void Run()
    {
        var app = Application.Current;
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Stickel;component/Themes/Minimal.xaml", UriKind.Relative) });
        var output = Path.GetFullPath("artifacts/theme-minimal"); Directory.CreateDirectory(output);
        foreach (var light in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        {
            Localization.Apply(rtl ? "fa" : "en", light);
            Check(app.FindResource("Selected") is string label && label == Localization.T("Selected"), "Preview labels stay localized text after theme changes");
            var root = new Grid { Width = 1080, Height = 720, Background = Brush("Bg"), FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
            TextElementDefaults(root);
            var columns = new UniformGrid { Columns = 3, Margin = new Thickness(28) }; root.Children.Add(columns);
            var settings = Panel("STICKEL"); columns.Children.Add(settings.Surface);
            settings.Content.Children.Add(new TextBlock { Text = rtl ? "تنظیمات خروجی" : "Output settings", FontSize = 20, Margin = new Thickness(8, 12, 8, 20) });
            var combo = new ComboBox { ItemsSource = new[] { "WEBM · VP9", "GIF", "MP4 · H.264", "PNG", "WEBP" }, SelectedIndex = 0 };
            settings.Content.Children.Add(combo);
            settings.Content.Children.Add(new TextBlock { Text = rtl ? "کیفیت خروجی" : "Output quality", Margin = new Thickness(8, 20, 8, 3) });
            var slider = new Slider { Minimum = 0, Maximum = 100, Value = 50 }; settings.Content.Children.Add(slider);
            settings.Content.Children.Add(new CheckBox { Content = rtl ? "خروجی بی‌صدا" : "Silent output", IsChecked = true });
            settings.Content.Children.Add(new CheckBox { Content = rtl ? "خروجی جداگانهٔ هر بخش" : "Export individual clips" });
            settings.Content.Children.Add(new TextBox { Text = "512 × 288", Margin = new Thickness(4, 15, 4, 10) });
            var primary = new Button { Content = rtl ? "خروجی گرفتن" : "Export stickers", Style = (Style)app.FindResource("PrimaryButton") }; IconAssist.SetKind(primary, "file-export"); settings.Content.Children.Add(primary);
            settings.Content.Children.Add(new Expander { Header = rtl ? "واترمارک مشترک" : "Watermark", IsExpanded = true, Content = new Slider { Value = 65 } });
            var controls = Panel(rtl ? "کنترل رسانه" : "Media controls"); columns.Children.Add(controls.Surface);
            var transport = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 20, 2, 14), HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var icon in new[] { "backward-step", "play", "stop", "forward-step" }) { var button = new Button { Width = 42, Height = 40, Padding = new Thickness(8) }; IconAssist.SetKind(button, icon); transport.Children.Add(button); }
            controls.Content.Children.Add(transport);
            controls.Content.Children.Add(new RadioButton { Content = rtl ? "بخش انتخاب‌شده" : "Selected clip", IsChecked = true });
            controls.Content.Children.Add(new RadioButton { Content = rtl ? "کل تایم‌لاین" : "Whole timeline" });
            var tabs = new TabControl { Height = 180, Margin = new Thickness(0, 18, 0, 10) };
            tabs.Items.Add(new TabItem { Header = rtl ? "ویدیو" : "Video", Content = new TextBlock { Text = "AVI · MP4 · WEBM", Margin = new Thickness(12) } });
            tabs.Items.Add(new TabItem { Header = rtl ? "تصاویر" : "Images" }); controls.Content.Children.Add(tabs);
            controls.Content.Children.Add(new Button { Content = rtl ? "غیرفعال" : "Disabled action", IsEnabled = false });
            controls.Content.Children.Add(new ProgressBar { Value = 60, Height = 6, Margin = new Thickness(10, 25, 10, 0) });
            var scrollPanel = Panel(rtl ? "رسانه‌ها" : "Media library"); columns.Children.Add(scrollPanel.Surface);
            var list = new StackPanel();
            for (var i = 1; i <= 15; i++) list.Children.Add(new TextBox { Text = (rtl ? "رسانهٔ " : "Media ") + i, Height = 36 });
            var scroll = new ScrollViewer { Content = list, Height = 430, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, Margin = new Thickness(4, 15, 4, 12) }; scrollPanel.Content.Children.Add(scroll);
            var horizontal = new ScrollBar { Orientation = Orientation.Horizontal, Minimum = 0, Maximum = 100, Value = 35, ViewportSize = 25 }; scrollPanel.Content.Children.Add(horizontal);
            Layout(root);

            Check(slider.Template.FindName("PART_Track", slider) is Track, "Slider has its native track");
            var track = (Track)slider.Template.FindName("PART_Track", slider);
            Check(track.Thumb?.ActualWidth == 20, "Slider has a round 20 px drag handle");
            slider.Value = 0; Layout(root); var start = track.Thumb!.TransformToAncestor(slider).Transform(new Point()).X;
            slider.Value = 100; Layout(root); var end = track.Thumb.TransformToAncestor(slider).Transform(new Point()).X;
            Check(Math.Abs(end - start) > 100, "Slider handle follows the complete value range");
            slider.Value = 50; Slider.DecreaseSmall.Execute(null, slider); Check(Math.Abs(slider.Value - (50 - slider.SmallChange)) < .0001, "Slider keyboard command still changes value");
            slider.Value = 65;
            Check(combo.Template.FindName("PART_Popup", combo) is Popup, "ComboBox retains its popup");
            var field = (TextBox)settings.Content.Children.OfType<TextBox>().First();
            Check(field.Template.FindName("PART_ContentHost", field) is ScrollViewer, "Text input retains its editable content host");
            Check(Descendants(transport).OfType<FaIcon>().Select(i => i.Kind).SequenceEqual(new[] { "backward-step", "play", "stop", "forward-step" }), "Transport icons receive their Font Awesome glyphs");
            var play = (Button)transport.Children[1]; IconAssist.SetKind(play, "pause"); Layout(root);
            Check(Descendants(play).OfType<FaIcon>().Single().Kind == "pause", "Playback glyph updates when switching from play to pause");
            IconAssist.SetKind(play, "play");
            var line = (TextBox)list.Children[4]; var host = (ScrollViewer)line.Template.FindName("PART_ContentHost", line);
            Check(host.ViewportHeight >= host.ExtentHeight, "Compact text inputs display the complete line without clipping");
            var vertical = Descendants(scroll).OfType<ScrollBar>().First(b => b.Orientation == Orientation.Vertical && ReferenceEquals(b.TemplatedParent, scroll));
            Check(vertical.Maximum > 0 && vertical.Track?.Thumb?.ActualHeight > 26, "Vertical scrollbar reflects its scrollable content");
            scroll.ScrollToVerticalOffset(140); Layout(root); Check(scroll.VerticalOffset == 140, "Themed ScrollViewer still scrolls");
            var hTrack = (Track)horizontal.Template.FindName("PART_Track", horizontal);
            Check(hTrack.Orientation == Orientation.Horizontal && hTrack.Thumb!.ActualWidth > 26, "Horizontal scrollbar keeps proportional draggable grip");
            Check(Brush("Panel").Color.A == 255 && Brush("Raised").Color.A == 255, "Panels and buttons use solid opaque fills");
            Check(Descendants(root).OfType<UIElement>().All(e => e.Effect is null) && Brush("Bg").Color == (light ? Colors.White : Colors.Black), "Background is pure white/black without decorative effects");
            CheckTextContrast(root, columns);
            Layout(root);
            var bitmap = new RenderTargetBitmap(1080, 720, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(output, $"{(light ? "light" : "dark")}-{(rtl ? "fa" : "en")}.png"); using var file = File.Create(path); png.Save(file);
            Console.WriteLine("RENDER " + path);
        }
        Console.WriteLine($"COMPLETE: {checks} theme checks.");
    }

    private static (Border Surface, StackPanel Content) Panel(string title)
    {
        var content = new StackPanel(); content.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 3, 8, 5) });
        return (new Border { Background = Brush("Panel"), BorderBrush = Brush("SoftEdge"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(16), Margin = new Thickness(10), Child = content }, content);
    }
    private static void CheckTextContrast(Grid root, UniformGrid columns)
    {
        foreach (Border panel in columns.Children) panel.Child.Visibility = Visibility.Hidden;
        Layout(root);
        var background = new RenderTargetBitmap(1080, 720, 96, 96, PixelFormats.Pbgra32); background.Render(root);
        var pixels = new byte[1080 * 720 * 4]; background.CopyPixels(pixels, 1080 * 4, 0);
        var minimum = double.MaxValue;
        foreach (Border panel in columns.Children)
        {
            var topLeft = panel.TransformToAncestor(root).Transform(new Point());
            for (var y = (int)topLeft.Y + 16; y < topLeft.Y + panel.ActualHeight - 16; y += 20)
            for (var x = (int)topLeft.X + 16; x < topLeft.X + panel.ActualWidth - 16; x += 20)
            {
                var i = (y * 1080 + x) * 4;
                var surface = new SolidColorBrush(Color.FromRgb(pixels[i + 2], pixels[i + 1], pixels[i]));
                minimum = Math.Min(minimum, Math.Min(Contrast(Brush("Text"), surface), Contrast(Brush("Muted"), surface)));
            }
            panel.Child.Visibility = Visibility.Visible;
        }
        Check(minimum >= 4.5, $"Text contrast on neutral panels is {minimum:F2}:1 (minimum 4.5:1)");
    }
    private static void TextElementDefaults(Grid root) { root.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, Brush("Text")); root.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, new FontFamily("Segoe UI, Tahoma")); root.SetValue(System.Windows.Documents.TextElement.FontSizeProperty, 13d); }
    private static SolidColorBrush Brush(string key) => (SolidColorBrush)Application.Current.FindResource(key);
    private static void Layout(FrameworkElement root) { root.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); root.Measure(new Size(root.Width, root.Height)); root.Arrange(new Rect(0, 0, root.Width, root.Height)); root.UpdateLayout(); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root) { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var descendant in Descendants(child)) yield return descendant; } }
    private static double Contrast(SolidColorBrush first, SolidColorBrush second) { double L(Color c) { double C(byte v) { var x = v / 255d; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); } return .2126 * C(c.R) + .7152 * C(c.G) + .0722 * C(c.B); } var a = L(first.Color); var b = L(second.Color); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; Console.WriteLine("PASS " + message); }
}
