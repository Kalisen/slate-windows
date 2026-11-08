using System.Runtime.InteropServices;
using Slate.App.Interop;
using Slate.Core.Geometry;
using Slate.Core.Windowing;
using Point = Slate.Core.Geometry.Point;
using Size = Slate.Core.Geometry.Size;

namespace Slate.App.Windowing;

/// <summary>A top-level window. Frames are the visible bounds, without the invisible resize borders of Windows 10/11.</summary>
internal sealed class Win32Window(nint handle, IApplication application) : IWindow
{
    public nint Handle { get; } = handle;
    public long Id => Handle;
    public string Title => NativeMethods.WindowText(Handle);
    public IApplication Application { get; } = application;

    public bool IsMinimizedOrHidden =>
        NativeMethods.IsIconic(Handle) || !NativeMethods.IsWindowVisible(Handle) || NativeMethods.IsCloaked(Handle);

    public Rect Frame
    {
        get
        {
            if (NativeMethods.IsIconic(Handle) && TryGetPlacement(out var placement)) return ToRect(RestoredBounds(placement));
            return ToRect(VisibleBounds(out _));
        }
    }

    public bool Move(Point topLeft) => SetBounds(topLeft, null);

    public bool Resize(Size size) => SetBounds(null, size);

    public bool Focus()
    {
        if (NativeMethods.IsIconic(Handle)) NativeMethods.ShowWindow(Handle, NativeMethods.SW_RESTORE);
        return Foreground.Activate(Handle);
    }

    private bool SetBounds(Point? topLeft, Size? size)
    {
        if (!NativeMethods.IsWindow(Handle) || NativeMethods.IsHungAppWindow(Handle)) return false;
        if (NativeMethods.IsIconic(Handle)) return SetRestoredBounds(topLeft, size);
        // A maximized window ignores SetWindowPos until it is restored.
        if (NativeMethods.IsZoomed(Handle)) NativeMethods.ShowWindow(Handle, NativeMethods.SW_RESTORE);

        RECT visible = VisibleBounds(out RECT window);
        int left = visible.Left - window.Left, top = visible.Top - window.Top;
        int right = window.Right - visible.Right, bottom = window.Bottom - visible.Bottom;
        uint flags = NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER;
        int x = 0, y = 0, cx = 0, cy = 0;
        if (topLeft is { } p)
        {
            x = (int)Math.Round(p.X) - left;
            y = (int)Math.Round(p.Y) - top;
        }
        else
        {
            flags |= NativeMethods.SWP_NOMOVE;
        }
        if (size is { } s)
        {
            cx = (int)Math.Round(s.Width) + left + right;
            cy = (int)Math.Round(s.Height) + top + bottom;
        }
        else
        {
            flags |= NativeMethods.SWP_NOSIZE;
        }
        return NativeMethods.SetWindowPos(Handle, 0, x, y, cx, cy, flags);
    }

    /// <summary>Minimized windows keep their restored bounds in the placement, in work-area coordinates.</summary>
    private bool SetRestoredBounds(Point? topLeft, Size? size)
    {
        if (!TryGetPlacement(out var placement)) return false;
        RECT bounds = RestoredBounds(placement);
        var (dx, dy) = WorkAreaOffset(bounds);
        int width = size is { } s ? (int)Math.Round(s.Width) : bounds.Width;
        int height = size is { } sz ? (int)Math.Round(sz.Height) : bounds.Height;
        int x = topLeft is { } p ? (int)Math.Round(p.X) : bounds.Left;
        int y = topLeft is { } pt ? (int)Math.Round(pt.Y) : bounds.Top;
        placement.NormalPosition = new RECT { Left = x - dx, Top = y - dy, Right = x - dx + width, Bottom = y - dy + height };
        return NativeMethods.SetWindowPlacement(Handle, ref placement);
    }

    private RECT RestoredBounds(WINDOWPLACEMENT placement)
    {
        RECT normal = placement.NormalPosition;
        var (dx, dy) = WorkAreaOffset(normal);
        return new RECT { Left = normal.Left + dx, Top = normal.Top + dy, Right = normal.Right + dx, Bottom = normal.Bottom + dy };
    }

    /// <summary>Offset from work-area to screen coordinates (non-zero when the taskbar is on the left or top).</summary>
    private (int Dx, int Dy) WorkAreaOffset(RECT bounds)
    {
        long exStyle = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GWL_EXSTYLE);
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0) return (0, 0);
        var screen = System.Windows.Forms.Screen.FromRectangle(new System.Drawing.Rectangle(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
        return (screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top);
    }

    private bool TryGetPlacement(out WINDOWPLACEMENT placement)
    {
        placement = new WINDOWPLACEMENT { Length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        return NativeMethods.GetWindowPlacement(Handle, ref placement);
    }

    private RECT VisibleBounds(out RECT window)
    {
        NativeMethods.GetWindowRect(Handle, out window);
        return NativeMethods.DwmGetWindowAttribute(Handle, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out RECT visible, Marshal.SizeOf<RECT>()) == 0
            ? visible
            : window;
    }

    private static Rect ToRect(RECT r) => new(r.Left, r.Top, r.Width, r.Height);

    public override bool Equals(object? obj) => obj is Win32Window other && other.Handle == Handle;

    public override int GetHashCode() => Handle.GetHashCode();
}

/// <summary>Brings a window to the foreground despite Windows' foreground lock.</summary>
internal static class Foreground
{
    public static bool Activate(nint handle)
    {
        // Windows lets the process that sent the last input change the foreground window: send an empty mouse input.
        var input = new INPUT[1];
        NativeMethods.SendInput(1, input, Marshal.SizeOf<INPUT>());
        if (NativeMethods.SetForegroundWindow(handle)) return true;

        uint foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        uint currentThread = NativeMethods.GetCurrentThreadId();
        if (foregroundThread == currentThread) return NativeMethods.SetForegroundWindow(handle);
        NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            NativeMethods.BringWindowToTop(handle);
            return NativeMethods.SetForegroundWindow(handle);
        }
        finally
        {
            NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
        }
    }
}
