using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Stickel.Desktop;

internal static class WindowTheme
{
    // Native caption attributes are supported on Windows 11; older Windows keeps its system caption.
    // https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute
    internal static void Apply(Window window, bool light)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var dark = light ? 0 : 1;
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        var caption = SystemParameters.HighContrast ? -1 : ColorRef((SolidColorBrush)window.FindResource("Bg"));
        var text = SystemParameters.HighContrast ? -1 : ColorRef((SolidColorBrush)window.FindResource("Text"));
        _ = DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
    }

    private static int ColorRef(SolidColorBrush brush) => brush.Color.R | brush.Color.G << 8 | brush.Color.B << 16;
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
