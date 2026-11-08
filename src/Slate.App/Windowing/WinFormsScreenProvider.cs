using Slate.App.Interop;
using Slate.Core.Geometry;
using Slate.Core.Screens;

namespace Slate.App.Windowing;

/// <summary>Screens in physical pixels (the process is per-monitor DPI aware), primary first.</summary>
internal sealed class WinFormsScreenProvider : IScreenProvider
{
    public IReadOnlyList<ScreenInfo> Screens()
    {
        var screens = Screen.AllScreens.OrderByDescending(s => s.Primary).ToList();
        return screens.Select((s, i) => new ScreenInfo(i, ToRect(s.Bounds), ToRect(s.WorkingArea), ScaleOf(s))).ToList();
    }

    private static Rect ToRect(System.Drawing.Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

    private static double ScaleOf(Screen screen)
    {
        var center = new POINT { X = screen.Bounds.X + screen.Bounds.Width / 2, Y = screen.Bounds.Y + screen.Bounds.Height / 2 };
        nint monitor = NativeMethods.MonitorFromPoint(center, NativeMethods.MONITOR_DEFAULTTONEAREST);
        return NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpi, out _) == 0
            ? dpi / (double)NativeMethods.DefaultDpi
            : 1.0;
    }
}
