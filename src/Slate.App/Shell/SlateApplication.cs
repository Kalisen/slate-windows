using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;
using Slate.App.Input;
using Slate.App.Overlays;
using Slate.App.Platform;
using Slate.App.Windowing;
using Slate.Core.Configuration;
using Slate.Core.Expressions;
using Slate.Core.Interaction;
using Slate.Core.Keyboard;
using Slate.Core.Layouts;
using Slate.Core.Operations;
using Slate.Core.Screens;
using Slate.Core.Snapshots;
using Binding = Slate.Core.Keyboard.Binding;

namespace Slate.App.Shell;

/// <summary>The tray application: loads the config, listens to the keyboard and runs bindings.</summary>
internal sealed class SlateApplication : ApplicationContext, IAppControl
{
    private const string RepositoryUrl = "https://github.com/Kalisen/slate-windows";
    /// <summary>Display changes arrive in bursts; wait for them to settle before checking default layouts.</summary>
    private static readonly TimeSpan ScreenChangeSettleDelay = TimeSpan.FromSeconds(1);

    private readonly SynchronizationContext ui;
    private readonly UiScheduler scheduler = new();
    private readonly WinFormsScreenProvider screens = new();
    private readonly Win32WindowSystem windows = new();
    private readonly SystemKeyboardLayout keyboardLayout = new();
    private readonly HintNumbers hintNumbers = new();
    private readonly HotkeyDispatcher dispatcher;
    private readonly KeyboardHook hook;
    private readonly NotifyIcon tray;
    private readonly Icon idleIcon = TrayIcons.Create(active: false);
    private readonly Icon modalIcon = TrayIcons.Create(active: true);
    private readonly ToolStripMenuItem launchOnLogin;
    private readonly ToolStripMenuItem activateSnapshot;
    private readonly SnapshotRepository snapshots = new(new SnapshotFileStore());
    private readonly OperationContext context;
    private readonly SlateConfiguration config;
    private string screenFingerprint;
    private IDisposable? pendingScreenCheck;
    private WindowInfoForm? windowInfo;

    public SlateApplication()
    {
        ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        dispatcher = new HotkeyDispatcher(scheduler) { RunBinding = RunBinding };
        dispatcher.ModalChanged += active => tray!.Icon = active ? modalIcon : idleIcon;
        hook = new KeyboardHook(dispatcher);

        launchOnLogin = new ToolStripMenuItem("Launch Slate on Login", null, (_, _) => ToggleLaunchOnLogin());
        activateSnapshot = new ToolStripMenuItem("Activate Snapshot", null, (_, _) => Run(new ActivateSnapshotOperation(SnapshotNames.Menu, delete: false)));
        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            new ToolStripMenuItem("About Slate", null, (_, _) => ShowAbout()),
            new ToolStripMenuItem("Relaunch and Load Config", null, (_, _) => Relaunch()),
            launchOnLogin,
            new ToolStripMenuItem("Take Snapshot", null, (_, _) => Run(new SnapshotOperation(SnapshotNames.Menu, saveToDisk: true, isStack: false))),
            activateSnapshot,
            new ToolStripMenuItem("Current Window Info", null, (_, _) => ShowWindowInfo()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Quit Slate", null, (_, _) => ExitThread()),
        ]);
        menu.Opening += (_, _) =>
        {
            launchOnLogin.Checked = StartupRegistration.IsEnabled;
            activateSnapshot.Enabled = snapshots.Has(SnapshotNames.Menu);
        };
        tray = new NotifyIcon { Icon = idleIcon, Text = AppPaths.AppName, ContextMenuStrip = menu, Visible = true };

        config = LoadConfiguration();
        context = new OperationContext(config.Settings, windows, screens, snapshots, config.Layouts, new ShellRunner(), this,
            new WinFormsUi(screens), dispatcher, scheduler, keyboardLayout, hintNumbers);
        screenFingerprint = context.Screens().Fingerprint();

        LoadSnapshots();
        UndoOperations.Clear().Execute(context);
        dispatcher.Load(config.Bindings, ModalEscapeKey(), new RepeatTiming(
            TimeSpan.FromSeconds(config.Settings.GetDouble(ConfigKeys.SecondsBeforeRepeat)),
            TimeSpan.FromSeconds(config.Settings.GetDouble(ConfigKeys.SecondsBetweenRepeat))));
        hook.Install();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        if (config.Settings.GetBool(ConfigKeys.CheckDefaultsOnLoad)) ActivateDefaultLayout();
        Log.Info($"Slate started with the {config.Source} configuration ({config.Bindings.All.Count()} bindings).");
    }

    public void Relaunch() => Relauncher.Relaunch();

    protected override void ExitThreadCore()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        hook.Dispose();
        tray.Visible = false;
        tray.Dispose();
        windowInfo?.Dispose();
        idleIcon.Dispose();
        modalIcon.Dispose();
        base.ExitThreadCore();
    }

    private SlateConfiguration LoadConfiguration()
    {
        var errors = new DialogConfigErrorReporter();
        var parser = new ConfigParser(new TrustedConfigFileSource(), keyboardLayout, AppPaths.HomeDirectory, errors);
        var loader = new ConfigLoader(parser, new ConfigApproval(new DpapiApprovalStore(), new DialogConfigApprover()), errors);
        return loader.Load();
    }

    private Keystroke? ModalEscapeKey()
    {
        string value = config.Settings.Get(ConfigKeys.ModalEscapeKey);
        if (value.Length == 0) return null;
        try
        {
            return BindingKeystroke.ParseSimple(value, keyboardLayout);
        }
        catch (ConfigException ex)
        {
            Log.Error($"Invalid {ConfigKeys.ModalEscapeKey}: {ex.Message}");
            return null;
        }
    }

    private void LoadSnapshots()
    {
        try
        {
            snapshots.Load();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            if (Dialogs.WarnAndAskQuit("Could not load snapshots", $"{ex.Message}{Environment.NewLine}Try deleting {Path.Combine(AppPaths.DataDirectory, AppPaths.SnapshotsFileName)}."))
                throw new ConfigLoadAbortedException();
        }
    }

    /// <summary>
    /// Runs a binding from the keyboard hook. It is queued so the hook returns quickly: Windows removes hooks that are
    /// slow to respond.
    /// </summary>
    private void RunBinding(Binding binding) => ui.Post(_ =>
    {
        Execute(binding.Operation);
        dispatcher.OnBindingExecuted(binding);
    }, null);

    private void Run(Operation operation) => Execute(operation);

    private void Execute(Operation operation)
    {
        try
        {
            if (config.UsesUndo && operation.TakesUndoSnapshot(config.Settings)) UndoOperations.Record(config.Settings).Execute(context);
            operation.Execute(context);
        }
        catch (Exception ex) when (ex is ConfigException or ExpressionException)
        {
            string title = ex is ConfigException ce ? ce.Title : "Invalid Expression";
            Log.Error($"{title}: {ex.Message}");
            if (Dialogs.WarnAndAskQuit(title, ex.Message)) ExitThread();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        pendingScreenCheck?.Dispose();
        pendingScreenCheck = scheduler.Schedule(ScreenChangeSettleDelay, () =>
        {
            pendingScreenCheck = null;
            string fingerprint = context.Screens().Fingerprint();
            if (fingerprint == screenFingerprint) return;
            screenFingerprint = fingerprint;
            Log.Info("Screen configuration changed.");
            ActivateDefaultLayout();
        });
    }

    /// <summary>Activates the layout or snapshot of the first <c>default</c> directive matching the screens.</summary>
    private void ActivateDefaultLayout()
    {
        DefaultLayout? match = DefaultLayout.FindMatch(config.Defaults, context.Screens());
        if (match is null) return;
        if (config.Layouts.TryGetValue(match.Name, out Layout? layout)) Execute(new LayoutOperation(layout.Name));
        else if (snapshots.Has(match.Name)) Execute(new ActivateSnapshotOperation(match.Name, delete: false));
    }

    private void ToggleLaunchOnLogin()
    {
        try
        {
            StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Log.Error($"Unable to change launch on login: {ex.Message}");
        }
    }

    private void ShowWindowInfo()
    {
        if (windowInfo is null || windowInfo.IsDisposed) windowInfo = new WindowInfoForm(() => WindowInfoReport.Build(context));
        windowInfo.ShowCentered();
    }

    private static void ShowAbout()
    {
        string version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        Dialogs.Info($"Slate {version.Split('+')[0]}",
            "A keyboard-driven window manager for Windows, ported from Slate for macOS." + Environment.NewLine + Environment.NewLine +
            "Copyright © 2011-2013 Jigish Patel. Windows port © 2026." + Environment.NewLine +
            "Window Hints idea by philc." + Environment.NewLine +
            "Licensed under the GNU General Public License v3." + Environment.NewLine + Environment.NewLine +
            RepositoryUrl);
    }
}
