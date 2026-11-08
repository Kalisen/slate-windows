using Slate.Core.Configuration;
using Slate.Core.Geometry;
using Slate.Core.Interaction;
using Slate.Core.Keyboard;
using Slate.Core.Operations;

namespace Slate.Core.Tests.Interaction;

public class HintTests
{
    private static Keystroke Press(char c) => new(UsKeyboardLayout.Instance.KeyForCharacter(c)!.Value, Modifiers.None);

    private static (TestEnvironment Env, FakeWindow Left, FakeWindow Right) TwoWindows()
    {
        var env = new TestEnvironment();
        var right = env.Windows.AddApp("Right").AddWindow("R", new Rect(1000, 100, 400, 400));
        var left = env.Windows.AddApp("Left").AddWindow("L", new Rect(100, 100, 400, 400));
        return (env, left, right);
    }

    [Fact]
    public void Hints_are_ordered_left_to_right_and_centered()
    {
        var (env, _, _) = TwoWindows();
        var hints = HintPlanner.Plan(env.Context, "AB");
        Assert.Equal(new[] { ('A', "L"), ('B', "R") }, hints.Select(h => (h.Label, h.Window.Title)));
        Assert.Equal(new Rect(250, 250, 100, 100), hints[0].Bounds);
    }

    [Theory]
    [InlineData("rightToLeft", "R")]
    [InlineData("none", "R")]
    [InlineData("leftToRight", "L")]
    public void The_order_setting_picks_the_first_hinted_window(string order, string first)
    {
        var (env, _, _) = TwoWindows();
        env.Settings.Set(ConfigKeys.WindowHintsOrder, order);
        Assert.Equal(first, HintPlanner.Plan(env.Context, "AB")[0].Window.Title);
    }

    [Fact]
    public void Persisted_hints_keep_their_character()
    {
        var (env, left, _) = TwoWindows();
        env.Settings.Set(ConfigKeys.WindowHintsOrder, "persist");
        char before = HintPlanner.Plan(env.Context, "AB").Single(h => h.Window.Id == left.Id).Label;
        env.Windows.Apps.Reverse();
        Assert.Equal(before, HintPlanner.Plan(env.Context, "AB").Single(h => h.Window.Id == left.Id).Label);
    }

    [Fact]
    public void Covered_windows_get_no_hint_unless_configured()
    {
        var (env, left, _) = TwoWindows();
        var cover = new FakeApplication("Cover", 9) { System = env.Windows };
        cover.AddWindow("C", new Rect(0, 0, 600, 600));
        env.Windows.Apps.Insert(0, cover);
        Assert.DoesNotContain(HintPlanner.Plan(env.Context, "ABC"), h => h.Window.Id == left.Id);

        env.Settings.Set(ConfigKeys.WindowHintsIgnoreHiddenWindows, "false");
        Assert.Contains(HintPlanner.Plan(env.Context, "ABC"), h => h.Window.Id == left.Id);
    }

    [Fact]
    public void Later_hint_positions_are_tried_when_the_first_is_covered()
    {
        var (env, left, _) = TwoWindows();
        env.Settings.Set(ConfigKeys.WindowHintsTopLeftX, "(windowSizeX/2)-(windowHintsWidth/2);0");
        env.Settings.Set(ConfigKeys.WindowHintsTopLeftY, "(windowSizeY/2)-(windowHintsHeight/2);0");
        var cover = new FakeApplication("Cover", 9) { System = env.Windows };
        cover.AddWindow("C", new Rect(200, 200, 200, 200));
        env.Windows.Apps.Insert(0, cover);
        Assert.Equal(new Rect(100, 100, 100, 100), HintPlanner.Plan(env.Context, "ABC").Single(h => h.Window.Id == left.Id).Bounds);
    }

    [Fact]
    public void Colliding_hints_spread_downwards()
    {
        var env = new TestEnvironment();
        env.Settings.Set(ConfigKeys.WindowHintsSpread, "true");
        env.Settings.Set(ConfigKeys.WindowHintsIgnoreHiddenWindows, "false");
        env.Windows.AddApp("A").AddWindow("A", new Rect(100, 100, 400, 400));
        env.Windows.AddApp("B").AddWindow("B", new Rect(105, 100, 400, 400));
        var hints = HintPlanner.Plan(env.Context, "AB");
        Assert.Equal(250 + 100 + 20, hints[1].Bounds.Y);
    }

    [Fact]
    public void Hint_sizes_follow_the_screen_scale()
    {
        var env = new TestEnvironment();
        env.Screens.List[0] = FakeScreens.Primary with { Scale = 1.5 };
        env.Windows.AddApp("A").AddWindow("A", new Rect(100, 100, 400, 400));
        var hint = HintPlanner.Plan(env.Context, "A")[0];
        Assert.Equal(new Rect(225, 225, 150, 150), hint.Bounds);
        Assert.Equal(1.5, hint.Scale);
    }

    [Fact]
    public void Characters_run_out()
    {
        var (env, _, _) = TwoWindows();
        Assert.Single(HintPlanner.Plan(env.Context, "A"));
    }

    [Fact]
    public void Pressing_a_hint_focuses_its_window()
    {
        var (env, _, right) = TwoWindows();
        env.Parse("hint AB").Execute(env.Context);
        var session = env.Sessions.Capture!;
        Assert.Equal(CaptureResult.PassThrough, session.OnKeyDown(Press('z'), false));
        Assert.Equal(CaptureResult.PassThrough, session.OnKeyDown(Press('b') with { Modifiers = Modifiers.Ctrl }, false));
        Assert.Equal(CaptureResult.Handled, session.OnKeyDown(Press('b'), false));
        Assert.True(right.Focused);
        Assert.True(env.Ui.HintsClosed);
        Assert.Null(env.Sessions.Capture);
    }

    [Fact]
    public void Escape_or_the_timeout_closes_the_hints()
    {
        var (env, _, _) = TwoWindows();
        env.Parse("hint").Execute(env.Context);
        Assert.Equal(TimeSpan.FromSeconds(3), env.Scheduler.NextDelay);
        env.Sessions.Capture!.OnKeyDown(new Keystroke(new Key(VirtualKeys.Escape), Modifiers.None), false);
        Assert.True(env.Ui.HintsClosed);

        var (env2, _, _) = TwoWindows();
        env2.Parse("hint").Execute(env2.Context);
        env2.Scheduler.RunNext();
        Assert.True(env2.Ui.HintsClosed);
        Assert.Null(env2.Sessions.Capture);
    }

    [Fact]
    public void Hint_numbers_are_stable_and_reused()
    {
        var numbers = new HintNumbers();
        Assert.Equal(0, numbers.NumberFor(10));
        Assert.Equal(1, numbers.NumberFor(20));
        Assert.Equal(0, numbers.NumberFor(10));
        numbers.Retain([20]);
        Assert.Equal(0, numbers.NumberFor(30));
        Assert.Equal(2, numbers.NumberFor(40));
    }
}

public class GridTests
{
    [Theory]
    [InlineData(5, 5, 5, 5, 0, 0, 1, 1)]
    [InlineData(5, 5, 199, 99, 0, 0, 2, 2)]
    [InlineData(199, 99, 5, 5, 0, 0, 2, 2)]
    [InlineData(150, 60, 500, 500, 1, 1, 1, 1)]
    public void Drags_select_cells(double bx, double by, double ex, double ey, int column, int row, int columns, int rows)
    {
        var geometry = new GridGeometry(new Size(200, 100), 2, 2, padding: 2);
        Assert.Equal(new GridSelection(3, column, row, columns, rows), geometry.Select(3, new Point(bx, by), new Point(ex, ey)));
    }

    [Fact]
    public void Cell_bounds_include_padding() =>
        Assert.Equal(new Rect(101, 2, 97, 47), new GridGeometry(new Size(200, 100), 2, 2, padding: 2).CellBounds(1, 0));

    [Fact]
    public void Parses_screen_grids_and_padding()
    {
        var op = GridOperation.Parse("grid padding:5 1920x1080:16,9 1:6,4 bad 2:x,1");
        Assert.Equal(5, op.Padding);
        Assert.Equal((16, 9), op.ScreenGrids["1920x1080"]);
        Assert.Equal((6, 4), op.ScreenGrids["1"]);
        Assert.Equal(2, op.ScreenGrids.Count);
    }

    [Fact]
    public void Selecting_cells_moves_the_window_and_closes_the_grids()
    {
        var env = new TestEnvironment();
        env.Screens.List.Add(FakeScreens.Right);
        var window = env.FocusedWindow(new Rect(10, 10, 100, 100));
        env.Parse("grid 1280x1024:4,2").Execute(env.Context);
        Assert.Equal(new[] { (12, 12), (4, 2) }, env.Ui.Grids!.Select(g => (g.Columns, g.Rows)));

        env.Ui.OnGridSelected!(new GridSelection(1, 1, 1, 2, 1));
        Assert.Equal(new Rect(1920 + 320, 512, 640, 512), window.Frame);
        Assert.True(window.Focused);
        Assert.True(env.Ui.GridClosed);
        Assert.Null(env.Sessions.Capture);
    }

    [Fact]
    public void Escape_closes_the_grids()
    {
        var env = new TestEnvironment();
        env.Parse("grid").Execute(env.Context);
        Assert.Equal(CaptureResult.PassThrough, env.Sessions.Capture!.OnKeyDown(new Keystroke(new Key(VirtualKeys.Tab), Modifiers.None), false));
        Assert.Equal(CaptureResult.Handled, env.Sessions.Capture!.OnKeyDown(new Keystroke(new Key(VirtualKeys.Escape), Modifiers.None), false));
        Assert.True(env.Ui.GridClosed);
    }
}

public class SwitchTests
{
    private static readonly Key Tab = new(VirtualKeys.Tab);
    private static readonly Key Backtick = new(VirtualKeys.Oem3);
    private static readonly Key Q = new(VirtualKeys.LetterA + 16);
    private static readonly Key F = new(VirtualKeys.LetterA + 5);
    private static readonly Key H = new(VirtualKeys.LetterA + 7);

    private static (TestEnvironment Env, IKeyboardCapture Session, FakeApplication[] Apps) Open(int count = 3)
    {
        var env = new TestEnvironment();
        var apps = Enumerable.Range(0, count).Select(i =>
        {
            var app = env.Windows.AddApp($"App{i}");
            app.AddWindow($"W{i}", new Rect(0, 0, 100, 100));
            return app;
        }).ToArray();
        var op = (SwitchOperation)env.Parse("switch");
        op.Trigger = new Keystroke(Tab, Modifiers.Alt);
        op.Execute(env.Context);
        return (env, env.Sessions.Capture!, apps);
    }

    private static CaptureResult Press(IKeyboardCapture session, Key key, Modifiers modifiers = Modifiers.Alt, bool repeat = false) =>
        session.OnKeyDown(new Keystroke(key, modifiers), repeat);

    [Fact]
    public void Opens_on_the_previous_application_and_cycles()
    {
        var (env, session, _) = Open();
        Assert.Equal(1, env.Ui.SwitcherSelected);
        Assert.Equal(CaptureResult.HandledRepeating, Press(session, Tab));
        Assert.Equal(2, env.Ui.SwitcherSelected);
        Press(session, Tab);
        Assert.Equal(0, env.Ui.SwitcherSelected);
        Press(session, Backtick);
        Assert.Equal(2, env.Ui.SwitcherSelected);
        Press(session, Tab, Modifiers.Alt | Modifiers.Shift);
        Assert.Equal(1, env.Ui.SwitcherSelected);
    }

    [Fact]
    public void Repeats_stop_at_the_edges()
    {
        var (env, session, _) = Open();
        Press(session, Tab, repeat: true);
        Press(session, Tab, repeat: true);
        Assert.Equal(2, env.Ui.SwitcherSelected);
        env.Settings.Set(ConfigKeys.SwitchStopRepeatAtEdge, "false");
        Press(session, Tab, repeat: true);
        Assert.Equal(0, env.Ui.SwitcherSelected);
    }

    [Fact]
    public void Releasing_the_modifiers_activates_the_selection()
    {
        var (env, session, apps) = Open();
        session.OnModifiersChanged(Modifiers.Alt | Modifiers.Shift);
        Assert.False(env.Ui.SwitcherClosed);
        session.OnModifiersChanged(Modifiers.None);
        Assert.True(env.Ui.SwitcherClosed);
        Assert.True(apps[1].ActivatedMainOnly);
        Assert.Null(env.Sessions.Capture);
    }

    [Fact]
    public void Marks_applications_to_quit_or_force_quit()
    {
        var (env, session, apps) = Open();
        Press(session, Q);
        Assert.True(env.Ui.SwitcherItems![1].Quitting);
        Press(session, F);
        Assert.True(env.Ui.SwitcherItems![1].ForceQuitting);
        Assert.False(env.Ui.SwitcherItems![1].Quitting);
        Press(session, Tab);
        Press(session, Q);
        Assert.Equal(CaptureResult.Handled, Press(session, Q, repeat: true));
        Assert.True(env.Ui.SwitcherItems![2].Quitting);
        session.OnModifiersChanged(Modifiers.None);
        Assert.True(apps[1].ForceQuitted);
        Assert.True(apps[2].Quitted);
        Assert.False(apps[0].Quitted);
        Assert.Null(apps[2].ActivatedMainOnly);
    }

    [Fact]
    public void Hide_toggles_the_selected_application()
    {
        var (env, session, apps) = Open();
        Press(session, H);
        Assert.True(apps[1].IsHidden);
        Assert.True(env.Ui.SwitcherItems![1].Hidden);
        Press(session, H);
        Assert.False(apps[1].IsHidden);
    }

    [Fact]
    public void Keys_without_the_trigger_modifiers_pass_through()
    {
        var (_, session, _) = Open();
        Assert.Equal(CaptureResult.PassThrough, Press(session, Tab, Modifiers.Ctrl));
        Assert.Equal(CaptureResult.PassThrough, Press(session, new Key(VirtualKeys.LetterA), Modifiers.Alt));
    }

    [Fact]
    public void Only_focuses_the_main_window_when_configured()
    {
        var (env, session, apps) = Open();
        env.Settings.Set(ConfigKeys.SwitchOnlyFocusMainWindow, "false");
        session.OnModifiersChanged(Modifiers.None);
        Assert.False(apps[1].ActivatedMainOnly);
    }
}

public class WindowInfoReportTests
{
    [Fact]
    public void Lists_screens_and_windows_in_config_terms()
    {
        var env = new TestEnvironment();
        env.FocusedWindow(new Rect(50, 60, 300, 200), "Editor", "notes.txt");
        string report = WindowInfoReport.Build(env.Context);
        Assert.Contains("Resolution: 1920x1080", report);
        Assert.Contains("Application: Editor", report);
        Assert.Contains("Window: 'notes.txt'", report);
        Assert.Contains("Top Left: (screenOriginX+50, screenOriginY+60)", report);
    }
}
