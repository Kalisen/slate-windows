using Slate.Core.Geometry;

namespace Slate.Core.Windowing;

/// <summary>A top-level application window that operations can move, resize and focus.</summary>
public interface IWindow
{
    /// <summary>Stable identity for the lifetime of the window (the HWND on Windows).</summary>
    long Id { get; }

    string Title { get; }

    IApplication Application { get; }

    /// <summary>Visible bounds in virtual-screen coordinates, excluding invisible resize borders.</summary>
    Rect Frame { get; }

    bool IsMinimizedOrHidden { get; }

    bool Move(Point topLeft);

    bool Resize(Size size);

    bool Focus();
}

/// <summary>A running application: a process owning one or more top-level windows.</summary>
public interface IApplication
{
    int ProcessId { get; }

    /// <summary>Display name used to match the application in configs, e.g. "Google Chrome".</summary>
    string Name { get; }

    /// <summary>True when every window of the application is minimized (Windows has no app-level hide).</summary>
    bool IsHidden { get; }

    /// <summary>Windows in front-to-back order; the first one is the application's main window.</summary>
    IReadOnlyList<IWindow> Windows { get; }

    /// <summary>True if <paramref name="name"/> designates this application (display or executable name).</summary>
    bool IsNamed(string name);

    void Hide();

    void Show();

    /// <summary>Brings the main window, or every window when <paramref name="mainWindowOnly"/> is false, to the front.</summary>
    bool Activate(bool mainWindowOnly);

    /// <summary>Politely asks the application to close all its windows.</summary>
    void Quit();

    void ForceQuit();
}

/// <summary>Access to the desktop's applications and windows.</summary>
public interface IWindowSystem
{
    /// <summary>The window that has keyboard focus, or null when none (e.g. the desktop).</summary>
    IWindow? FocusedWindow();

    /// <summary>Running applications with at least one window, most recently used first.</summary>
    IReadOnlyList<IApplication> Applications();

    /// <summary>The top-level window under <paramref name="point"/>, if any.</summary>
    IWindow? WindowAt(Point point);
}

/// <summary>The identity of a window state used to remember the position of a window in a <c>chain</c>.</summary>
public readonly record struct WindowState(int ProcessId, Rect Frame)
{
    public static WindowState Of(IWindow window) => new(window.Application.ProcessId, window.Frame);
}
