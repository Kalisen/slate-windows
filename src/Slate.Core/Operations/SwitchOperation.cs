using Slate.Core.Configuration;
using Slate.Core.Interaction;
using Slate.Core.Keyboard;
using Slate.Core.Text;
using Slate.Core.Windowing;

namespace Slate.Core.Operations;

/// <summary>Keys acting on the selected application while the switcher is open (pressed with the binding's modifiers).</summary>
public sealed record SwitchKeys(Key Back, Key Quit, Key ForceQuit, Key Hide)
{
    public const string BackOption = "back";
    public const string QuitOption = "quit";
    public const string ForceQuitOption = "force-quit";
    public const string HideOption = "hide";

    public const string DefaultBack = "`";
    public const string DefaultQuit = "q";
    public const string DefaultForceQuit = "f";
    public const string DefaultHide = "h";
}

/// <summary>
/// <c>switch [back:key] [quit:key] [force-quit:key] [hide:key]</c>: an application switcher. While the binding's modifiers
/// are held, its key selects the next application; releasing the modifiers switches to the selection.
/// </summary>
public sealed class SwitchOperation(SwitchKeys keys) : Operation
{
    public override OperationKind Kind => OperationKind.Switch;
    public SwitchKeys Keys { get; } = keys;

    /// <summary>The keystroke of the binding that opens the switcher.</summary>
    public Keystroke Trigger { get; set; }

    public static SwitchOperation Parse(string text, ParseContext context)
    {
        Key Resolve(string name) => KeyNames.Parse(name, context.KeyboardLayout)
            ?? throw new ConfigException("Unrecognized Key", $"Unrecognized key \"{name}\" in \"{text}\"");
        var keys = new SwitchKeys(
            Resolve(SwitchKeys.DefaultBack), Resolve(SwitchKeys.DefaultQuit), Resolve(SwitchKeys.DefaultForceQuit), Resolve(SwitchKeys.DefaultHide));
        foreach (string option in StringTokenizer.Tokenize(text).Skip(1))
        {
            string[] parts = option.Split(':');
            if (parts.Length != 2 || KeyNames.Parse(parts[1], context.KeyboardLayout) is not { } key) continue;
            keys = parts[0] switch
            {
                SwitchKeys.BackOption => keys with { Back = key },
                SwitchKeys.QuitOption => keys with { Quit = key },
                SwitchKeys.ForceQuitOption => keys with { ForceQuit = key },
                SwitchKeys.HideOption => keys with { Hide = key },
                _ => keys,
            };
        }
        return new SwitchOperation(keys);
    }

    public override bool Execute(OperationContext context) => ExecuteOn(context, null);

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        var apps = context.Windows.Applications();
        if (apps.Count == 0) return false;
        var session = new SwitchSession(context, apps, Keys, Trigger);
        context.Keyboard.BeginCapture(session);
        return true;
    }
}

/// <summary>The switcher's state while it is open.</summary>
public sealed class SwitchSession : IKeyboardCapture
{
    private readonly OperationContext context;
    private readonly SwitchKeys keys;
    private readonly Keystroke trigger;
    private readonly SwitcherItem[] items;
    private readonly ISwitcherView view;
    private bool open = true;

    public SwitchSession(OperationContext context, IReadOnlyList<IApplication> apps, SwitchKeys keys, Keystroke trigger)
    {
        this.context = context;
        this.keys = keys;
        this.trigger = trigger;
        items = apps.Select(a => new SwitcherItem(a, a.IsHidden, false, false)).ToArray();
        Selected = items.Length > 1 ? 1 : 0;
        view = context.Ui.ShowSwitcher(items, Selected, context.Settings);
    }

    public int Selected { get; private set; }

    public IReadOnlyList<SwitcherItem> Items => items;

    public (TimeSpan Before, TimeSpan Between) RepeatDelays => (
        TimeSpan.FromSeconds(context.Settings.GetDouble(ConfigKeys.SwitchSecondsBeforeRepeat)),
        TimeSpan.FromSeconds(context.Settings.GetDouble(ConfigKeys.SwitchSecondsBetweenRepeat)));

    public CaptureResult OnKeyDown(Keystroke keystroke, bool isRepeat)
    {
        if ((keystroke.Modifiers & trigger.Modifiers) != trigger.Modifiers) return CaptureResult.PassThrough;
        bool backwards = (keystroke.Modifiers & ~trigger.Modifiers) == Modifiers.Shift;
        Key key = keystroke.Key;
        if (key == trigger.Key || key == keys.Back)
        {
            Move(backwards ^ (key == keys.Back) ? -1 : 1, isRepeat);
            return CaptureResult.HandledRepeating;
        }
        if (isRepeat) return CaptureResult.Handled;
        SwitcherItem item = items[Selected];
        if (key == keys.Quit) items[Selected] = item with { Quitting = !item.Quitting, ForceQuitting = false };
        else if (key == keys.ForceQuit) items[Selected] = item with { ForceQuitting = !item.ForceQuitting, Quitting = false };
        else if (key == keys.Hide) ToggleHidden();
        else return CaptureResult.PassThrough;
        view.Update(items, Selected);
        return CaptureResult.Handled;
    }

    /// <summary>Releasing the binding's modifiers switches to the selected application.</summary>
    public void OnModifiersChanged(Modifiers held)
    {
        if ((held & trigger.Modifiers) != trigger.Modifiers) Commit();
    }

    public void Cancel() => Close();

    private void Move(int step, bool isRepeat)
    {
        bool stopAtEdge = isRepeat && context.Settings.GetBool(ConfigKeys.SwitchStopRepeatAtEdge);
        int next = Selected + step;
        if (next < 0) next = stopAtEdge ? 0 : items.Length - 1;
        else if (next >= items.Length) next = stopAtEdge ? items.Length - 1 : 0;
        Selected = next;
        view.Update(items, Selected);
    }

    private void ToggleHidden()
    {
        SwitcherItem item = items[Selected];
        if (item.Hidden) item.Application.Activate(context.Settings.GetBool(ConfigKeys.SwitchOnlyFocusMainWindow));
        else item.Application.Hide();
        items[Selected] = item with { Hidden = !item.Hidden };
    }

    private void Commit()
    {
        if (!open) return;
        Close();
        foreach (SwitcherItem item in items)
        {
            if (item.ForceQuitting) item.Application.ForceQuit();
            else if (item.Quitting) item.Application.Quit();
        }
        SwitcherItem selected = items[Selected];
        if (selected.Quitting || selected.ForceQuitting) return;
        if (selected.Application.IsHidden) selected.Application.Show();
        selected.Application.Activate(context.Settings.GetBool(ConfigKeys.SwitchOnlyFocusMainWindow));
    }

    private void Close()
    {
        if (!open) return;
        open = false;
        view.Close();
        context.Keyboard.EndCapture(this);
    }
}
