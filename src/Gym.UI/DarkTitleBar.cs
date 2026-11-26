using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Gym.UI;

/// <summary>Asks Windows 10/11 for a dark title bar in the sidebar colour. Ignored where unsupported.</summary>
public static class DarkTitleBar
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCaptionColor = 35;
    private const int CaptionColorRef = 0x00171311; // #111317 as 0x00BBGGRR

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            var on = 1;
            var color = CaptionColorRef;
            _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref on, sizeof(int));
            _ = DwmSetWindowAttribute(handle, DwmCaptionColor, ref color, sizeof(int));
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
