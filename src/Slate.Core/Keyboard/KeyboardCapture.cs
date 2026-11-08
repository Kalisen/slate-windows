namespace Slate.Core.Keyboard;

/// <summary>What a capture did with a key press.</summary>
public enum CaptureResult
{
    /// <summary>The key is not for the capture: let it through to the focused application.</summary>
    PassThrough,
    /// <summary>The key was consumed.</summary>
    Handled,
    /// <summary>The key was consumed and should repeat while held.</summary>
    HandledRepeating,
}

/// <summary>
/// An interactive overlay (hints, grid, switcher) that takes over the keyboard while it is shown. Captures are only
/// called on the UI thread.
/// </summary>
public interface IKeyboardCapture
{
    CaptureResult OnKeyDown(Keystroke keystroke, bool isRepeat);

    /// <summary>Called whenever the held modifiers change.</summary>
    void OnModifiersChanged(Modifiers held);

    /// <summary>Delay before a held key first repeats, then between repeats.</summary>
    (TimeSpan Before, TimeSpan Between) RepeatDelays { get; }

    /// <summary>Closes the capture without acting, e.g. when another one replaces it.</summary>
    void Cancel();
}

public interface IKeyboardSessions
{
    /// <summary>Routes keys to <paramref name="capture"/> until it ends; a previous capture is cancelled.</summary>
    void BeginCapture(IKeyboardCapture capture);

    void EndCapture(IKeyboardCapture capture);
}
