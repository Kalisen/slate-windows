using Slate.Core.Configuration;
using Slate.Core.Geometry;
using Slate.Core.Interaction;
using Slate.Core.Keyboard;
using Slate.Core.Layouts;
using Slate.Core.Operations;
using Slate.Core.Screens;
using Slate.Core.Snapshots;
using Slate.Core.Windowing;

namespace Slate.Core.Tests;

internal sealed class FakeWindow : IWindow
{
    private static long nextId = 1;

    public FakeWindow(FakeApplication app, string title, Rect frame)
    {
        Application = app;
        Title = title;
        Frame = frame;
    }

    public long Id { get; } = Interlocked.Increment(ref nextId);
    public string Title { get; set; }
    public IApplication Application { get; }
    public Rect Frame { get; set; }
    public bool IsMinimizedOrHidden { get; set; }
    public bool Focused { get; private set; }
    /// <summary>Smallest size the window accepts, like an application enforcing a minimum size.</summary>
    public Size MinimumSize { get; set; } = new(0, 0);
    public bool Fails { get; set; }

    public bool Move(Point topLeft)
    {
        if (Fails) return false;
        Frame = Frame with { X = topLeft.X, Y = topLeft.Y };
        return true;
    }

    public bool Resize(Size size)
    {
        if (Fails) return false;
        Frame = Frame with { Width = Math.Max(size.Width, MinimumSize.Width), Height = Math.Max(size.Height, MinimumSize.Height) };
        return true;
    }

    public bool Focus()
    {
        Focused = true;
        ((FakeApplication)Application).System?.SetFocus(this);
        return true;
    }
}

internal sealed class FakeApplication(string name, int processId) : IApplication
{
    public FakeWindowSystem? System { get; set; }
    public int ProcessId { get; } = processId;
    public string Name { get; } = name;
    public List<FakeWindow> WindowList { get; } = [];
    public IReadOnlyList<IWindow> Windows => WindowList;
    public bool IsHidden { get; private set; }
    public bool Quitted { get; private set; }
    public bool ForceQuitted { get; private set; }
    public bool? ActivatedMainOnly { get; private set; }

    public FakeWindow AddWindow(string title, Rect frame)
    {
        var window = new FakeWindow(this, title, frame);
        WindowList.Add(window);
        return window;
    }

    public bool IsNamed(string name) => string.Equals(name, Name, StringComparison.OrdinalIgnoreCase);
    public void Hide() => IsHidden = true;
    public void Show() => IsHidden = false;

    public bool Activate(bool mainWindowOnly)
    {
        ActivatedMainOnly = mainWindowOnly;
        IsHidden = false;
        return WindowList.Count > 0 && WindowList[0].Focus();
    }

    public void Quit() => Quitted = true;
    public void ForceQuit() => ForceQuitted = true;
}

internal sealed class FakeWindowSystem : IWindowSystem
{
    private int nextPid = 100;

    public List<FakeApplication> Apps { get; } = [];
    public IWindow? Focused { get; set; }

    public FakeApplication AddApp(string name)
    {
        var app = new FakeApplication(name, nextPid++) { System = this };
        Apps.Add(app);
        return app;
    }

    public void SetFocus(IWindow window) => Focused = window;
    public IWindow? FocusedWindow() => Focused;
    public IReadOnlyList<IApplication> Applications() => Apps;

    /// <summary>The front-most window containing the point: windows are front to back in app then window order.</summary>
    public IWindow? WindowAt(Point point) =>
        Apps.SelectMany(a => a.WindowList).FirstOrDefault(w => !w.IsMinimizedOrHidden && w.Frame.Contains(point));
}

internal sealed class FakeScreens(params ScreenInfo[] screens) : IScreenProvider
{
    public List<ScreenInfo> List { get; } = [.. screens];
    public IReadOnlyList<ScreenInfo> Screens() => List;

    /// <summary>A 1920x1080 primary screen with a 40px taskbar at the bottom.</summary>
    public static ScreenInfo Primary => new(0, new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1040));

    /// <summary>A 1280x1024 screen right of the primary one, without taskbar.</summary>
    public static ScreenInfo Right => new(1, new Rect(1920, 0, 1280, 1024), new Rect(1920, 0, 1280, 1024));
}

/// <summary>A scheduler driven by the test: <see cref="RunNext"/> fires the earliest pending callback.</summary>
internal sealed class ManualScheduler : IScheduler
{
    private readonly List<Entry> pending = [];

    public int PendingCount => pending.Count(e => !e.Cancelled);

    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var entry = new Entry(delay, action);
        pending.Add(entry);
        return entry;
    }

    public TimeSpan? NextDelay => pending.FirstOrDefault(e => !e.Cancelled)?.Delay;

    public void RunNext()
    {
        Entry entry = pending.First(e => !e.Cancelled);
        pending.Remove(entry);
        entry.Action();
    }

    private sealed class Entry(TimeSpan delay, Action action) : IDisposable
    {
        public TimeSpan Delay { get; } = delay;
        public Action Action { get; } = action;
        public bool Cancelled { get; private set; }
        public void Dispose() => Cancelled = true;
    }
}

internal sealed class FakeUi : IInteractiveUi
{
    public List<HintPlacement>? Hints { get; private set; }
    public bool HintsClosed { get; private set; }
    public IReadOnlyList<GridSpec>? Grids { get; private set; }
    public Action<GridSelection>? OnGridSelected { get; private set; }
    public bool GridClosed { get; private set; }
    public IReadOnlyList<SwitcherItem>? SwitcherItems { get; private set; }
    public int SwitcherSelected { get; private set; }
    public bool SwitcherClosed { get; private set; }

    public IHintView ShowHints(IReadOnlyList<HintPlacement> hints, SlateSettings settings)
    {
        Hints = [.. hints];
        return new View(() => HintsClosed = true);
    }

    public IGridView ShowGrid(IReadOnlyList<GridSpec> grids, SlateSettings settings, Action<GridSelection> onSelected)
    {
        Grids = grids;
        OnGridSelected = onSelected;
        return new View(() => GridClosed = true);
    }

    public ISwitcherView ShowSwitcher(IReadOnlyList<SwitcherItem> items, int selected, SlateSettings settings)
    {
        SwitcherItems = [.. items];
        SwitcherSelected = selected;
        return new Switcher(this);
    }

    private sealed class View(Action onClose) : IHintView, IGridView
    {
        public void Close() => onClose();
    }

    private sealed class Switcher(FakeUi ui) : ISwitcherView
    {
        public void Update(IReadOnlyList<SwitcherItem> items, int selected)
        {
            ui.SwitcherItems = [.. items];
            ui.SwitcherSelected = selected;
        }

        public void Close() => ui.SwitcherClosed = true;
    }
}

internal sealed class RecordingShell : IShell
{
    public List<(string Command, IReadOnlyList<string> Arguments, bool Wait, string? Directory)> Runs { get; } = [];

    public bool Run(string command, IReadOnlyList<string> arguments, bool wait, string? workingDirectory)
    {
        Runs.Add((command, arguments, wait, workingDirectory));
        return true;
    }
}

internal sealed class RecordingAppControl : IAppControl
{
    public int Relaunches { get; private set; }
    public void Relaunch() => Relaunches++;
}

internal sealed class MemorySnapshotStore : ISnapshotStore
{
    public string? Json { get; set; }
    public string? Load() => Json;
    public void Save(string json) => Json = json;
}

internal sealed class RecordingSessions : IKeyboardSessions
{
    public IKeyboardCapture? Capture { get; private set; }
    public void BeginCapture(IKeyboardCapture capture) => Capture = capture;

    public void EndCapture(IKeyboardCapture capture)
    {
        if (Capture == capture) Capture = null;
    }
}

/// <summary>Everything an operation needs, backed by fakes.</summary>
internal sealed class TestEnvironment
{
    public const string Home = @"C:\Users\tester";

    public SlateSettings Settings { get; } = new();
    public FakeWindowSystem Windows { get; } = new();
    public FakeScreens Screens { get; } = new(FakeScreens.Primary);
    public MemorySnapshotStore SnapshotStore { get; } = new();
    public SnapshotRepository Snapshots { get; }
    public Dictionary<string, Layout> Layouts { get; } = [];
    public RecordingShell Shell { get; } = new();
    public RecordingAppControl App { get; } = new();
    public FakeUi Ui { get; } = new();
    public RecordingSessions Sessions { get; } = new();
    public ManualScheduler Scheduler { get; } = new();
    public HintNumbers HintNumbers { get; } = new();

    public TestEnvironment()
    {
        Snapshots = new SnapshotRepository(SnapshotStore);
    }

    public OperationContext Context => new(Settings, Windows, Screens, Snapshots, Layouts, Shell, App, Ui, Sessions, Scheduler,
        UsKeyboardLayout.Instance, HintNumbers);

    public ParseContext ParseContext => new(Settings, Layouts, UsKeyboardLayout.Instance, Home);

    public Operation Parse(string operation) => OperationParser.Parse(operation, ParseContext);

    /// <summary>Adds an application with one window and focuses it.</summary>
    public FakeWindow FocusedWindow(Rect frame, string app = "Editor", string title = "Untitled")
    {
        var window = Windows.AddApp(app).AddWindow(title, frame);
        Windows.Focused = window;
        return window;
    }
}
