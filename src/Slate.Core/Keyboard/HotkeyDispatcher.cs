using Slate.Core.Operations;

namespace Slate.Core.Keyboard;

/// <summary>Timing of repeat-on-hold bindings.</summary>
public sealed record RepeatTiming(TimeSpan Before, TimeSpan Between);

/// <summary>
/// Turns raw key events into binding activations: global bindings, modal mode, captures (hints, grid, switcher) and
/// repeat-on-hold. Every method runs on the UI thread and returns true when the key must be swallowed.
/// </summary>
public sealed class HotkeyDispatcher(IScheduler scheduler) : IKeyboardSessions
{
    private readonly HashSet<Key> swallowed = [];
    private KeyBindings bindings = new();
    private Keystroke? modalEscape;
    private RepeatTiming repeatTiming = new(TimeSpan.FromSeconds(0.4), TimeSpan.FromSeconds(0.2));
    private IKeyboardCapture? capture;
    private IDisposable? repeatTimer;
    private Key? repeatingKey;
    private Modifiers held;

    /// <summary>
    /// Runs a binding (undo snapshot, error reporting); set by the application. It may run the binding later, and then
    /// calls <see cref="OnBindingExecuted"/>.
    /// </summary>
    public Action<Binding> RunBinding { get; set; } = _ => { };

    /// <summary>Raised with true when modal mode starts and false when it ends.</summary>
    public event Action<bool>? ModalChanged;

    /// <summary>The keystroke that started the current modal mode, if any.</summary>
    public Keystroke? ActiveModal { get; private set; }

    public bool HasCapture => capture is not null;

    public void Load(KeyBindings newBindings, Keystroke? newModalEscape, RepeatTiming timing)
    {
        bindings = newBindings;
        modalEscape = newModalEscape;
        repeatTiming = timing;
        ExitModal();
    }

    /// <summary>Whether any binding uses <paramref name="keystroke"/>: lets the hook ignore unrelated keys cheaply.</summary>
    public bool IsBound(Keystroke keystroke) =>
        bindings.Global.Any(b => b.Keystroke.Keystroke == keystroke) || bindings.Modal.ContainsKey(keystroke);

    public bool OnKeyDown(Key key, Modifiers held, bool isAutoRepeat)
    {
        this.held = held;
        if (isAutoRepeat) return swallowed.Contains(key);
        StopRepeat();
        var keystroke = new Keystroke(key, held);
        bool handled = Dispatch(keystroke);
        if (handled) swallowed.Add(key);
        return handled;
    }

    /// <summary>
    /// Called once a binding has run. When it opened a capture that repeats (the switcher) and its key is still held,
    /// the held key starts repeating into the capture.
    /// </summary>
    public void OnBindingExecuted(Binding binding)
    {
        Keystroke keystroke = binding.Keystroke.Keystroke;
        if (repeatingKey is null && swallowed.Contains(keystroke.Key) &&
            capture is { } opened && opened.RepeatDelays.Before != Timeout.InfiniteTimeSpan)
            StartCaptureRepeat(opened, keystroke);
    }

    public bool OnKeyUp(Key key)
    {
        if (repeatingKey == key) StopRepeat();
        return swallowed.Remove(key);
    }

    public void OnModifiersChanged(Modifiers held)
    {
        this.held = held;
        StopRepeat();
        capture?.OnModifiersChanged(held);
    }

    /// <summary>
    /// Starts routing keys to <paramref name="capture"/>. It is told the modifiers held right now: captures may open
    /// after their binding's keys were already released.
    /// </summary>
    public void BeginCapture(IKeyboardCapture capture)
    {
        IKeyboardCapture? previous = this.capture;
        this.capture = capture;
        if (previous is not null && previous != capture) previous.Cancel();
        capture.OnModifiersChanged(held);
    }

    public void EndCapture(IKeyboardCapture capture)
    {
        if (this.capture != capture) return;
        this.capture = null;
        StopRepeat();
    }

    private bool Dispatch(Keystroke keystroke)
    {
        if (capture is { } active)
        {
            CaptureResult result = active.OnKeyDown(keystroke, isRepeat: false);
            if (result == CaptureResult.HandledRepeating) StartCaptureRepeat(active, keystroke);
            return result != CaptureResult.PassThrough;
        }

        if (ActiveModal is { } modal)
        {
            if (keystroke == modal || keystroke == modalEscape)
            {
                ExitModal();
                return true;
            }
            Binding? modalBinding = keystroke.Modifiers == Modifiers.None
                ? bindings.Modal[modal].FirstOrDefault(b => b.Keystroke.Keystroke.Key == keystroke.Key)
                : null;
            if (modalBinding is not null)
            {
                RunBinding(modalBinding);
                if (!modalBinding.Keystroke.Toggle) ExitModal();
                return true;
            }
        }
        else if (bindings.Modal.ContainsKey(keystroke))
        {
            ActiveModal = keystroke;
            ModalChanged?.Invoke(true);
            return true;
        }

        Binding? binding = bindings.Global.FirstOrDefault(b => b.Keystroke.Keystroke == keystroke);
        if (binding is null) return false;
        swallowed.Add(keystroke.Key);
        if (binding.RepeatOnHold) StartBindingRepeat(binding, keystroke.Key, repeatTiming.Before);
        RunBinding(binding);
        return true;
    }

    private void ExitModal()
    {
        if (ActiveModal is null) return;
        ActiveModal = null;
        ModalChanged?.Invoke(false);
    }

    private void StartBindingRepeat(Binding binding, Key key, TimeSpan delay)
    {
        repeatingKey = key;
        repeatTimer = scheduler.Schedule(delay, () =>
        {
            RunBinding(binding);
            StartBindingRepeat(binding, key, repeatTiming.Between);
        });
    }

    private void StartCaptureRepeat(IKeyboardCapture target, Keystroke keystroke, bool first = true)
    {
        var (before, between) = target.RepeatDelays;
        repeatingKey = keystroke.Key;
        repeatTimer = scheduler.Schedule(first ? before : between, () =>
        {
            if (capture != target) return;
            if (target.OnKeyDown(keystroke, isRepeat: true) == CaptureResult.HandledRepeating)
                StartCaptureRepeat(target, keystroke, first: false);
        });
    }

    private void StopRepeat()
    {
        repeatTimer?.Dispose();
        repeatTimer = null;
        repeatingKey = null;
    }
}
