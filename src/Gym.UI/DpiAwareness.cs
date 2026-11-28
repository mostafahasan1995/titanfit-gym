using System.Runtime.InteropServices;

namespace Gym.UI;

/// <summary>
/// The DPI awareness Windows gave the process. The apps ask for per-monitor V2 in <c>app.manifest</c>: WPF then draws
/// sharp at every display scaling (100 %, 125 %, 150 %) and follows a window moved to a monitor with another scaling.
/// Checked by <c>--self-test</c> and written into the smoke screenshot manifest.
/// </summary>
public static class DpiAwareness
{
    private static readonly IntPtr PerMonitorV2 = new(-4);
    private static readonly IntPtr PerMonitor = new(-3);
    private static readonly IntPtr SystemAware = new(-2);
    private static readonly IntPtr Unaware = new(-1);

    /// <summary>"PerMonitorV2", "PerMonitor", "System", "Unaware" or "Unknown".</summary>
    public static string Current()
    {
        try
        {
            var context = GetThreadDpiAwarenessContext();
            if (AreDpiAwarenessContextsEqual(context, PerMonitorV2))
            {
                return "PerMonitorV2";
            }

            if (AreDpiAwarenessContextsEqual(context, PerMonitor))
            {
                return "PerMonitor";
            }

            if (AreDpiAwarenessContextsEqual(context, SystemAware))
            {
                return "System";
            }

            return AreDpiAwarenessContextsEqual(context, Unaware) ? "Unaware" : "Unknown";
        }
        catch (EntryPointNotFoundException)
        {
            return "Unknown";
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetThreadDpiAwarenessContext();

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
}
