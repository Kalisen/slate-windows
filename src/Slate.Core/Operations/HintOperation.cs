using Slate.Core.Configuration;
using Slate.Core.Interaction;
using Slate.Core.Keyboard;
using Slate.Core.Text;
using Slate.Core.Windowing;

namespace Slate.Core.Operations;

/// <summary><c>hint [characters]</c>: labels windows with a character; pressing it focuses the window.</summary>
public sealed class HintOperation(string characters) : Operation
{
    private HintSession? active;

    public override OperationKind Kind => OperationKind.Hint;
    public string Characters { get; } = characters;

    public static HintOperation Parse(string text)
    {
        var tokens = StringTokenizer.Tokenize(text, 2);
        return new HintOperation(tokens.Count > 1 ? tokens[1] : HintPlanner.DefaultCharacters);
    }

    public override bool Execute(OperationContext context) => ExecuteOn(context, null);

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        if (active is { IsOpen: true }) return true;
        var placements = HintPlanner.Plan(context, Characters);
        IHintView view = context.Ui.ShowHints(placements, context.Settings);
        active = new HintSession(context, placements, view);
        context.Keyboard.BeginCapture(active);
        return true;
    }
}

/// <summary>Keyboard handling while hints are shown: a hint character focuses its window, Esc or the timeout closes.</summary>
public sealed class HintSession : IKeyboardCapture
{
    private readonly OperationContext context;
    private readonly IHintView view;
    private readonly Dictionary<Key, IWindow> windowsByKey = [];
    private readonly IDisposable timeout;

    public HintSession(OperationContext context, IReadOnlyList<HintPlacement> placements, IHintView view)
    {
        this.context = context;
        this.view = view;
        foreach (HintPlacement hint in placements)
        {
            if (KeyNames.Parse(char.ToLowerInvariant(hint.Label).ToString(), context.KeyboardLayout) is { } key)
                windowsByKey.TryAdd(key, hint.Window);
        }
        double seconds = context.Settings.GetDouble(ConfigKeys.WindowHintsDuration);
        timeout = context.Scheduler.Schedule(TimeSpan.FromSeconds(seconds), Close);
    }

    public bool IsOpen { get; private set; } = true;

    public (TimeSpan Before, TimeSpan Between) RepeatDelays => (Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    public CaptureResult OnKeyDown(Keystroke keystroke, bool isRepeat)
    {
        if (keystroke.Modifiers != Modifiers.None) return CaptureResult.PassThrough;
        if (keystroke.Key.VirtualKey == VirtualKeys.Escape)
        {
            Close();
            return CaptureResult.Handled;
        }
        if (!windowsByKey.TryGetValue(keystroke.Key, out IWindow? window)) return CaptureResult.PassThrough;
        Close();
        window.Focus();
        return CaptureResult.Handled;
    }

    public void OnModifiersChanged(Modifiers held) { }

    public void Cancel() => Close();

    private void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        timeout.Dispose();
        view.Close();
        context.Keyboard.EndCapture(this);
    }
}
