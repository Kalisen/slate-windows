using Slate.Core.Configuration;
using Slate.Core.Interaction;
using Slate.Core.Keyboard;
using Slate.Core.Layouts;
using Slate.Core.Screens;
using Slate.Core.Snapshots;
using Slate.Core.Windowing;

namespace Slate.Core.Operations;

/// <summary>Starts external programs for the <c>shell</c> operation.</summary>
public interface IShell
{
    /// <summary>Starts <paramref name="command"/>; blocks until it exits when <paramref name="wait"/> is set.</summary>
    bool Run(string command, IReadOnlyList<string> arguments, bool wait, string? workingDirectory);
}

/// <summary>Application-level actions operations can trigger.</summary>
public interface IAppControl
{
    void Relaunch();
}

/// <summary>Runs callbacks later on the UI thread.</summary>
public interface IScheduler
{
    IDisposable Schedule(TimeSpan delay, Action action);
}

/// <summary>Everything operations need at run time.</summary>
public sealed class OperationContext(
    SlateSettings settings,
    IWindowSystem windows,
    IScreenProvider screens,
    SnapshotRepository snapshots,
    IReadOnlyDictionary<string, Layout> layouts,
    IShell shell,
    IAppControl app,
    IInteractiveUi ui,
    IKeyboardSessions keyboard,
    IScheduler scheduler,
    IKeyboardLayout keyboardLayout,
    HintNumbers hintNumbers)
{
    public SlateSettings Settings { get; } = settings;
    public IWindowSystem Windows { get; } = windows;
    public SnapshotRepository Snapshots { get; } = snapshots;
    public IReadOnlyDictionary<string, Layout> Layouts { get; } = layouts;
    public IShell Shell { get; } = shell;
    public IAppControl App { get; } = app;
    public IInteractiveUi Ui { get; } = ui;
    public IKeyboardSessions Keyboard { get; } = keyboard;
    public IScheduler Scheduler { get; } = scheduler;
    public IKeyboardLayout KeyboardLayout { get; } = keyboardLayout;
    public HintNumbers HintNumbers { get; } = hintNumbers;

    /// <summary>The current screens, numbered according to the settings.</summary>
    public ScreenLayout Screens() => new(
        screens.Screens(),
        Settings.GetBool(ConfigKeys.OrderScreensLeftToRight),
        Settings.GetBool(ConfigKeys.DefaultToCurrentScreen));
}
