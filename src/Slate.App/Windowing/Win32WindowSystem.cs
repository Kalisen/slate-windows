using System.Diagnostics;
using Slate.App.Interop;
using Slate.Core.Geometry;
using Slate.Core.Windowing;
using Point = Slate.Core.Geometry.Point;

namespace Slate.App.Windowing;

/// <summary>An application: the windows of one process, front to back.</summary>
internal sealed class Win32Application : IApplication
{
    /// <summary>Shows a minimized window without activating it.</summary>
    private const int ShowNoActivate = 4;

    private readonly List<Win32Window> windows = [];

    public Win32Application(int processId, ProcessDetails details)
    {
        ProcessId = processId;
        Details = details;
    }

    public int ProcessId { get; }
    public ProcessDetails Details { get; }
    public string Name => Details.Name;
    public IReadOnlyList<IWindow> Windows => windows;
    public bool IsHidden => windows.Count > 0 && windows.All(w => NativeMethods.IsIconic(w.Handle));

    internal void Add(nint handle) => windows.Add(new Win32Window(handle, this));

    public bool IsNamed(string name) =>
        string.Equals(name, Name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, Details.ExecutableName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, Details.ExecutableName + ".exe", StringComparison.OrdinalIgnoreCase);

    public void Hide()
    {
        foreach (var window in windows) NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_MINIMIZE);
    }

    public void Show()
    {
        foreach (var window in windows.Where(w => NativeMethods.IsIconic(w.Handle)))
            NativeMethods.ShowWindow(window.Handle, ShowNoActivate);
    }

    public bool Activate(bool mainWindowOnly)
    {
        if (windows.Count == 0) return false;
        if (!mainWindowOnly)
        {
            for (int i = windows.Count - 1; i > 0; i--)
            {
                if (NativeMethods.IsIconic(windows[i].Handle)) NativeMethods.ShowWindow(windows[i].Handle, ShowNoActivate);
                NativeMethods.BringWindowToTop(windows[i].Handle);
            }
        }
        return windows[0].Focus();
    }

    public void Quit()
    {
        foreach (var window in windows) NativeMethods.PostMessage(window.Handle, NativeMethods.WM_CLOSE, 0, 0);
    }

    public void ForceQuit()
    {
        try
        {
            using var process = Process.GetProcessById(ProcessId);
            process.Kill();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Log.Warn($"Unable to force quit {Name}: {ex.Message}");
        }
    }
}

/// <summary>Enumerates the windows a user can switch to (those shown by Alt+Tab).</summary>
/// <param name="excludedProcessId">Windows of this process are ignored: by default Slate's own overlays and dialogs.</param>
internal sealed class Win32WindowSystem(int? excludedProcessId = null) : IWindowSystem
{
    private const string UwpFrameClass = "ApplicationFrameWindow";
    private const string UwpCoreClass = "Windows.UI.Core.CoreWindow";

    /// <summary>Shell windows that look like application windows but are part of the desktop.</summary>
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW",
    };

    private readonly int ownProcessId = excludedProcessId ?? Environment.ProcessId;

    public IReadOnlyList<IApplication> Applications()
    {
        var apps = new List<Win32Application>();
        var byPid = new Dictionary<int, Win32Application>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!IsUserWindow(hwnd)) return true;
            int pid = ProcessIdOf(hwnd);
            if (pid == ownProcessId) return true;
            if (!byPid.TryGetValue(pid, out var app))
            {
                app = new Win32Application(pid, ProcessInfoCache.For(pid));
                byPid[pid] = app;
                apps.Add(app);
            }
            app.Add(hwnd);
            return true;
        }, 0);
        return apps;
    }

    public IWindow? FocusedWindow()
    {
        nint hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == 0 || ShellClasses.Contains(NativeMethods.ClassName(hwnd))) return null;
        int pid = ProcessIdOf(hwnd);
        if (pid == ownProcessId) return null;
        // Prefer the window as part of its full application, so "current" acts on every window of the application.
        return Applications().SelectMany(a => a.Windows).FirstOrDefault(w => w.Id == hwnd) ?? Wrap(hwnd, pid);
    }

    public IWindow? WindowAt(Point point)
    {
        nint hwnd = NativeMethods.WindowFromPoint(new POINT { X = (int)point.X, Y = (int)point.Y });
        if (hwnd == 0) return null;
        nint root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        return root == 0 ? null : Wrap(root, ProcessIdOf(root));
    }

    private static Win32Window Wrap(nint hwnd, int pid)
    {
        var app = new Win32Application(pid, ProcessInfoCache.For(pid));
        app.Add(hwnd);
        return (Win32Window)app.Windows[0];
    }

    /// <summary>Visible (or minimized), titled, unowned, non-tool windows that are not cloaked on another desktop.</summary>
    private static bool IsUserWindow(nint hwnd)
    {
        if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsCloaked(hwnd)) return false;
        if (NativeMethods.GetWindowTextLength(hwnd) == 0) return false;
        long exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        bool appWindow = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0 && !appWindow) return false;
        if (NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) != 0 && !appWindow) return false;
        return !ShellClasses.Contains(NativeMethods.ClassName(hwnd));
    }

    /// <summary>The process owning a window; for UWP apps, the app's process rather than ApplicationFrameHost.</summary>
    private static int ProcessIdOf(nint hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (NativeMethods.ClassName(hwnd) == UwpFrameClass)
        {
            nint core = NativeMethods.FindWindowEx(hwnd, 0, UwpCoreClass, null);
            if (core != 0) NativeMethods.GetWindowThreadProcessId(core, out pid);
        }
        return (int)pid;
    }
}
