using Slate.Core.Configuration;
using Slate.Core.Keyboard;
using Slate.Core.Operations;

namespace Slate.Core.Tests.Keyboard;

public class HotkeyDispatcherTests
{
    private static readonly Key Up = new(VirtualKeys.Up);
    private static readonly Key One = new(VirtualKeys.Digit0 + 1);
    private static readonly Key Two = new(VirtualKeys.Digit0 + 2);
    private static readonly Key F4 = new(VirtualKeys.F1 + 3);
    private static readonly Key Esc = new(VirtualKeys.Escape);
    private static readonly TimeSpan Before = TimeSpan.FromSeconds(0.4);
    private static readonly TimeSpan Between = TimeSpan.FromSeconds(0.2);

    private sealed class Harness
    {
        public ManualScheduler Scheduler { get; } = new();
        public HotkeyDispatcher Dispatcher { get; }
        public List<string> Ran { get; } = [];
        public List<bool> ModalChanges { get; } = [];

        public Harness(string config, string modalEscape = "")
        {
            var env = new TestEnvironment();
            var bindings = new KeyBindings();
            foreach (string line in config.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                bindings.Add(Binding.Parse(line.Trim(), env.ParseContext));
            Dispatcher = new HotkeyDispatcher(Scheduler) { RunBinding = b => Ran.Add(b.Text) };
            Dispatcher.ModalChanged += ModalChanges.Add;
            Keystroke? escape = modalEscape.Length > 0 ? BindingKeystroke.ParseSimple(modalEscape, UsKeyboardLayout.Instance) : null;
            Dispatcher.Load(bindings, escape, new RepeatTiming(Before, Between));
        }
    }

    [Fact]
    public void Runs_and_swallows_bound_keystrokes_only()
    {
        var h = new Harness("bind up:ctrl push up");
        Assert.True(h.Dispatcher.OnKeyDown(Up, Modifiers.Ctrl, isAutoRepeat: false));
        Assert.False(h.Dispatcher.OnKeyDown(Up, Modifiers.Ctrl | Modifiers.Shift, isAutoRepeat: false));
        Assert.False(h.Dispatcher.OnKeyDown(Up, Modifiers.None, isAutoRepeat: false));
        Assert.Equal(new[] { "bind up:ctrl push up" }, h.Ran);
    }

    [Fact]
    public void Swallows_the_auto_repeat_and_release_of_handled_keys()
    {
        var h = new Harness("bind up:ctrl push up");
        h.Dispatcher.OnKeyDown(Up, Modifiers.Ctrl, false);
        Assert.True(h.Dispatcher.OnKeyDown(Up, Modifiers.Ctrl, isAutoRepeat: true));
        Assert.True(h.Dispatcher.OnKeyUp(Up));
        Assert.False(h.Dispatcher.OnKeyUp(Up));
        Assert.Single(h.Ran);
    }

    [Fact]
    public void Repeats_repeatable_operations_while_held()
    {
        var h = new Harness("bind up:ctrl resize +0 -10%");
        h.Dispatcher.OnKeyDown(Up, Modifiers.Ctrl, false);
        Assert.Equal(Before, h.Scheduler.NextDelay);
        h.Scheduler.RunNext();
        Assert.Equal(Between, h.Scheduler.NextDelay);
        h.Scheduler.RunNext();
        Assert.Equal(3, h.Ran.Count);
        h.Dispatcher.OnKeyUp(Up);
        Assert.Equal(0, h.Scheduler.PendingCount);
    }

    [Fact]
    public void Releasing_a_modifier_stops_the_repeat()
    {
        var h = new Harness("bind up:ctrl nudge +0 -10");
        h.Dispatcher.OnKeyDown(Up, Modifiers.Ctrl, false);
        h.Dispatcher.OnModifiersChanged(Modifiers.None);
        Assert.Equal(0, h.Scheduler.PendingCount);
    }

    [Fact]
    public void Non_repeatable_operations_run_once() =>
        Assert.Equal(0, Pending("bind up:ctrl push up"));

    private static int Pending(string config)
    {
        var h = new Harness(config);
        h.Dispatcher.OnKeyDown(Up, Modifiers.Ctrl, false);
        return h.Scheduler.PendingCount;
    }

    [Fact]
    public void Modal_mode_runs_one_binding_then_ends()
    {
        var h = new Harness("bind 1:f4,ctrl push up\nbind 2:f4,ctrl push down");
        Assert.False(h.Dispatcher.OnKeyDown(One, Modifiers.None, false));
        Assert.True(h.Dispatcher.OnKeyDown(F4, Modifiers.Ctrl, false));
        Assert.Equal(new[] { true }, h.ModalChanges);
        Assert.True(h.Dispatcher.OnKeyDown(Two, Modifiers.None, false));
        Assert.Equal(new[] { "bind 2:f4,ctrl push down" }, h.Ran);
        Assert.Equal(new[] { true, false }, h.ModalChanges);
        Assert.False(h.Dispatcher.OnKeyDown(One, Modifiers.None, false));
    }

    [Fact]
    public void Toggle_modal_mode_stays_on_until_the_modal_key_or_escape_key()
    {
        var h = new Harness("bind 1:f4:toggle push up", modalEscape: "esc");
        h.Dispatcher.OnKeyDown(F4, Modifiers.None, false);
        h.Dispatcher.OnKeyDown(One, Modifiers.None, false);
        h.Dispatcher.OnKeyDown(One, Modifiers.None, false);
        Assert.Equal(2, h.Ran.Count);
        Assert.True(h.Dispatcher.OnKeyDown(Esc, Modifiers.None, false));
        Assert.Null(h.Dispatcher.ActiveModal);

        h.Dispatcher.OnKeyDown(F4, Modifiers.None, false);
        h.Dispatcher.OnKeyDown(F4, Modifiers.None, false);
        Assert.Null(h.Dispatcher.ActiveModal);
    }

    [Fact]
    public void Global_bindings_still_work_in_modal_mode()
    {
        var h = new Harness("bind 1:f4 push up\nbind up:ctrl push down");
        h.Dispatcher.OnKeyDown(F4, Modifiers.None, false);
        Assert.True(h.Dispatcher.OnKeyDown(Up, Modifiers.Ctrl, false));
        Assert.Equal(new[] { "bind up:ctrl push down" }, h.Ran);
    }

    private sealed class Capture(CaptureResult result) : IKeyboardCapture
    {
        public List<(Keystroke, bool)> Keys { get; } = [];
        public List<Modifiers> ModifierChanges { get; } = [];
        public bool Cancelled { get; private set; }
        public (TimeSpan Before, TimeSpan Between) RepeatDelays => (Before, Between);

        public CaptureResult OnKeyDown(Keystroke keystroke, bool isRepeat)
        {
            Keys.Add((keystroke, isRepeat));
            return result;
        }

        public void OnModifiersChanged(Modifiers held) => ModifierChanges.Add(held);
        public void Cancel() => Cancelled = true;
    }

    [Fact]
    public void Captures_receive_keys_instead_of_bindings()
    {
        var h = new Harness("bind up:ctrl push up");
        var capture = new Capture(CaptureResult.HandledRepeating);
        h.Dispatcher.BeginCapture(capture);
        Assert.True(h.Dispatcher.OnKeyDown(Up, Modifiers.Ctrl, false));
        Assert.Empty(h.Ran);
        h.Scheduler.RunNext();
        Assert.Equal(new[] { (new Keystroke(Up, Modifiers.Ctrl), false), (new Keystroke(Up, Modifiers.Ctrl), true) }, capture.Keys);
        h.Dispatcher.OnModifiersChanged(Modifiers.Shift);
        Assert.Equal(new[] { Modifiers.None, Modifiers.Shift }, capture.ModifierChanges);
    }

    [Fact]
    public void Captures_can_let_keys_through_and_be_replaced()
    {
        var h = new Harness("");
        var first = new Capture(CaptureResult.PassThrough);
        h.Dispatcher.BeginCapture(first);
        Assert.False(h.Dispatcher.OnKeyDown(Up, Modifiers.None, false));
        h.Dispatcher.BeginCapture(new Capture(CaptureResult.Handled));
        Assert.True(first.Cancelled);
        Assert.True(h.Dispatcher.OnKeyDown(Up, Modifiers.None, false));
        Assert.Equal(0, h.Scheduler.PendingCount);
    }

    private static (HotkeyDispatcher Dispatcher, ManualScheduler Scheduler, List<Binding> Pending) DeferredSwitch()
    {
        var env = new TestEnvironment();
        var bindings = new KeyBindings();
        bindings.Add(Binding.Parse("bind tab:alt switch", env.ParseContext));
        var scheduler = new ManualScheduler();
        var pending = new List<Binding>();
        var dispatcher = new HotkeyDispatcher(scheduler) { RunBinding = pending.Add };
        dispatcher.Load(bindings, null, new RepeatTiming(Before, Between));
        return (dispatcher, scheduler, pending);
    }

    [Fact]
    public void A_deferred_binding_that_opens_a_repeating_capture_repeats_into_it_while_held()
    {
        var (dispatcher, scheduler, pending) = DeferredSwitch();
        var capture = new Capture(CaptureResult.HandledRepeating);
        dispatcher.OnKeyDown(new Key(VirtualKeys.Tab), Modifiers.Alt, false);

        dispatcher.BeginCapture(capture);
        dispatcher.OnBindingExecuted(pending[0]);
        Assert.Equal(new[] { Modifiers.Alt }, capture.ModifierChanges);
        Assert.Equal(Before, scheduler.NextDelay);
        scheduler.RunNext();
        Assert.True(Assert.Single(capture.Keys).Item2);
    }

    [Fact]
    public void A_capture_opened_after_its_keys_were_released_learns_it_immediately()
    {
        var (dispatcher, scheduler, pending) = DeferredSwitch();
        var capture = new Capture(CaptureResult.HandledRepeating);
        dispatcher.OnKeyDown(new Key(VirtualKeys.Tab), Modifiers.Alt, false);
        dispatcher.OnKeyUp(new Key(VirtualKeys.Tab));
        dispatcher.OnModifiersChanged(Modifiers.None);

        dispatcher.BeginCapture(capture);
        dispatcher.OnBindingExecuted(pending[0]);
        Assert.Equal(new[] { Modifiers.None }, capture.ModifierChanges);
        Assert.Equal(0, scheduler.PendingCount);
    }
}
