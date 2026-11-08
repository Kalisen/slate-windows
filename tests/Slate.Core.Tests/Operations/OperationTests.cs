using Slate.Core.Configuration;
using Slate.Core.Geometry;
using Slate.Core.Operations;

namespace Slate.Core.Tests.Operations;

public class ResizeOperationTests
{
    private static readonly Rect Start = new(100, 100, 800, 600);

    [Theory]
    [InlineData("resize +10% +0", "screenSize", 100, 100, 992, 600)]
    [InlineData("resize -10% -10%", "screenSize", 100, 100, 608, 496)]
    [InlineData("resize +10% +0", "windowSize", 100, 100, 880, 600)]
    [InlineData("resize +100 -100", "screenSize", 100, 100, 900, 500)]
    [InlineData("resize +100 +100 top-right", "screenSize", 0, 100, 900, 700)]
    [InlineData("resize +100 +100 bottom-left", "screenSize", 100, 0, 900, 700)]
    [InlineData("resize -100 -100 bottom-right", "screenSize", 200, 200, 700, 500)]
    public void Resizes_around_the_anchor(string operation, string percentOf, double x, double y, double w, double h)
    {
        var env = new TestEnvironment();
        env.Settings.Set(ConfigKeys.ResizePercentOf, percentOf);
        var window = env.FocusedWindow(Start);
        Assert.True(env.Parse(operation).Execute(env.Context));
        Assert.Equal(new Rect(x, y, w, h), window.Frame);
    }

    [Fact]
    public void Resizing_by_nothing_reports_failure()
    {
        var env = new TestEnvironment();
        env.FocusedWindow(Start);
        Assert.False(env.Parse("resize +0 +0").Execute(env.Context));
    }

    [Theory]
    [InlineData("resize +10%")]
    [InlineData("resize +10% +0 middle")]
    [InlineData("resize +abc +0")]
    public void Invalid_resizes_throw(string operation) =>
        Assert.Throws<ConfigException>(() => new TestEnvironment().Parse(operation));
}

public class CompositeOperationTests
{
    [Fact]
    public void Chain_runs_the_next_operation_for_the_window_each_time()
    {
        var env = new TestEnvironment();
        var window = env.FocusedWindow(new Rect(100, 100, 800, 600));
        var chain = env.Parse("chain push up | push right | push down");
        var positions = Enumerable.Range(0, 4).Select(_ =>
        {
            chain.Execute(env.Context);
            return window.Frame.TopLeft;
        }).ToList();
        Assert.Equal(new[] { new Point(100, 0), new Point(1120, 0), new Point(1120, 440), new Point(1120, 0) }, positions);
    }

    [Fact]
    public void Chain_restarts_for_a_window_it_has_not_moved()
    {
        var env = new TestEnvironment();
        var window = env.FocusedWindow(new Rect(100, 100, 800, 600));
        var chain = env.Parse("chain push up | push right");
        chain.Execute(env.Context);
        window.Frame = new Rect(300, 300, 800, 600);
        chain.Execute(env.Context);
        Assert.Equal(new Point(300, 0), window.Frame.TopLeft);
    }

    [Fact]
    public void Sequence_targets_the_window_focused_at_the_start_of_each_group()
    {
        var env = new TestEnvironment();
        var left = env.FocusedWindow(new Rect(100, 100, 400, 300), "Left");
        var right = env.Windows.AddApp("Right").AddWindow("R", new Rect(550, 100, 400, 300));
        env.Parse("sequence focus right > push left | push down").Execute(env.Context);
        Assert.Equal(new Point(0, 100), left.Frame.TopLeft);
        Assert.Equal(new Point(550, 740), right.Frame.TopLeft);
    }

    [Theory]
    [InlineData("chain")]
    [InlineData("chain push up | nope")]
    [InlineData("sequence")]
    public void Invalid_composites_throw(string operation) =>
        Assert.Throws<ConfigException>(() => new TestEnvironment().Parse(operation));
}

public class FocusOperationTests
{
    [Theory]
    [InlineData("focus right", "Right")]
    [InlineData("focus left", "Left")]
    [InlineData("focus up", "Above")]
    [InlineData("focus above", "Above")]
    [InlineData("focus down", "Below")]
    [InlineData("focus below", "Below")]
    [InlineData("focus behind", "Behind")]
    public void Focuses_the_window_in_a_direction(string operation, string expected)
    {
        var env = new TestEnvironment();
        var center = env.FocusedWindow(new Rect(600, 400, 600, 300), "Center");
        env.Windows.AddApp("Right").AddWindow("Right", new Rect(1250, 400, 300, 300));
        env.Windows.AddApp("Left").AddWindow("Left", new Rect(200, 400, 350, 300));
        env.Windows.AddApp("Above").AddWindow("Above", new Rect(600, 50, 600, 300));
        env.Windows.AddApp("Below").AddWindow("Below", new Rect(600, 750, 600, 200));
        env.Windows.AddApp("Behind").AddWindow("Behind", new Rect(650, 450, 400, 200));
        Assert.True(env.Parse(operation).Execute(env.Context));
        Assert.Equal(expected, env.Windows.Focused!.Title);
        Assert.NotEqual(center, env.Windows.Focused);
    }

    [Fact]
    public void Prefers_windows_of_the_same_application()
    {
        var env = new TestEnvironment();
        var app = env.Windows.AddApp("Editor");
        env.Windows.Focused = app.AddWindow("Main", new Rect(0, 0, 500, 500));
        env.Windows.AddApp("Other").AddWindow("Big", new Rect(510, 0, 500, 500));
        app.AddWindow("Small", new Rect(550, 0, 100, 100));
        env.Parse("focus right").Execute(env.Context);
        Assert.Equal("Small", env.Windows.Focused!.Title);

        env.Settings.Set(ConfigKeys.FocusPreferSameApp, "false");
        env.Windows.Focused = app.WindowList[0];
        env.Parse("focus right").Execute(env.Context);
        Assert.Equal("Big", env.Windows.Focused!.Title);
    }

    [Fact]
    public void Skips_minimized_and_untitled_windows_and_widens_the_probe()
    {
        var env = new TestEnvironment();
        env.Settings.Set(ConfigKeys.FocusCheckWidthMax, "300");
        env.FocusedWindow(new Rect(0, 0, 500, 500));
        env.Windows.AddApp("Hidden").AddWindow("Hidden", new Rect(520, 0, 100, 100)).IsMinimizedOrHidden = true;
        env.Windows.AddApp("Untitled").AddWindow("", new Rect(520, 0, 100, 100));
        env.Windows.AddApp("Far").AddWindow("Far", new Rect(750, 0, 100, 100));
        Assert.True(env.Parse("focus right").Execute(env.Context));
        Assert.Equal("Far", env.Windows.Focused!.Title);
    }

    [Fact]
    public void Focuses_an_application_main_window()
    {
        var env = new TestEnvironment();
        env.FocusedWindow(new Rect(0, 0, 100, 100));
        var target = env.Windows.AddApp("Google Chrome");
        target.AddWindow("Tab", new Rect(0, 0, 100, 100));
        Assert.True(env.Parse("focus 'Google Chrome'").Execute(env.Context));
        Assert.True(target.ActivatedMainOnly);
        Assert.False(env.Parse("focus 'Not Running'").Execute(env.Context));
    }

    [Fact]
    public void Unknown_directions_throw() => Assert.Throws<ConfigException>(() => new TestEnvironment().Parse("focus sideways"));
}

public class VisibilityOperationTests
{
    private static (TestEnvironment Env, FakeApplication Current, FakeApplication Chrome, FakeApplication Term) Setup()
    {
        var env = new TestEnvironment();
        env.FocusedWindow(new Rect(0, 0, 10, 10), "Editor");
        var current = env.Windows.Apps[0];
        var chrome = env.Windows.AddApp("Google Chrome");
        var term = env.Windows.AddApp("Terminal");
        return (env, current, chrome, term);
    }

    [Theory]
    [InlineData("hide 'Google Chrome','Terminal'", false, true, true)]
    [InlineData("hide current", true, false, false)]
    [InlineData("hide all", true, true, true)]
    [InlineData("hide all-but:'Terminal'", true, true, false)]
    [InlineData("hide all-but:current", false, true, true)]
    public void Hide(string operation, bool current, bool chrome, bool term)
    {
        var (env, c, g, t) = Setup();
        env.Parse(operation).Execute(env.Context);
        Assert.Equal((current, chrome, term), (c.IsHidden, g.IsHidden, t.IsHidden));
    }

    [Fact]
    public void Show_and_toggle()
    {
        var (env, _, chrome, term) = Setup();
        chrome.Hide();
        env.Parse("show 'Google Chrome'").Execute(env.Context);
        Assert.False(chrome.IsHidden);
        env.Parse("toggle 'Google Chrome','Terminal'").Execute(env.Context);
        Assert.True(chrome.IsHidden);
        Assert.True(term.IsHidden);
        env.Parse("toggle 'Terminal'").Execute(env.Context);
        Assert.False(term.IsHidden);
    }
}

public class SimpleOperationTests
{
    [Fact]
    public void Shell_parses_options_and_quoted_commands()
    {
        var env = new TestEnvironment();
        var op = Assert.IsType<ShellOperation>(env.Parse(@"shell wait path:~/code '""C:\Program Files\App\app.exe"" --open file.txt'"));
        Assert.Equal(@"C:\Program Files\App\app.exe", op.Command);
        Assert.Equal(new[] { "--open", "file.txt" }, op.Arguments);
        Assert.True(op.Wait);
        Assert.Equal(Path.Combine(TestEnvironment.Home, "code"), op.WorkingDirectory);

        op.Execute(env.Context);
        Assert.Single(env.Shell.Runs);
    }

    [Fact]
    public void Shell_expands_the_home_directory_in_the_command()
    {
        var op = Assert.IsType<ShellOperation>(new TestEnvironment().Parse("shell '~/bin/tool.exe'"));
        Assert.Equal(Path.Combine(TestEnvironment.Home, @"bin/tool.exe"), op.Command);
        Assert.False(op.Wait);
        Assert.Null(op.WorkingDirectory);
    }

    [Theory]
    [InlineData("shell 'notepad.exe'")]
    [InlineData(@"shell '.\tool.exe'")]
    [InlineData(@"shell 'bin\tool.exe'")]
    [InlineData("shell")]
    public void Shell_rejects_relative_commands(string operation) =>
        Assert.Throws<ConfigException>(() => new TestEnvironment().Parse(operation));

    [Fact]
    public void Relaunch_asks_the_application()
    {
        var env = new TestEnvironment();
        env.Parse("relaunch").Execute(env.Context);
        Assert.Equal(1, env.App.Relaunches);
    }

    [Fact]
    public void Layout_must_be_defined_first() =>
        Assert.Equal("Unrecognized Layout", Assert.Throws<ConfigException>(() => new TestEnvironment().Parse("layout missing")).Title);

    [Fact]
    public void Unknown_operations_throw() =>
        Assert.Equal("Unrecognized Operation", Assert.Throws<ConfigException>(() => new TestEnvironment().Parse("teleport")).Title);

    [Theory]
    [InlineData("push up", true)]
    [InlineData("focus left", false)]
    [InlineData("snapshot x", false)]
    [InlineData("activate-snapshot x", true)]
    public void Undo_snapshots_are_taken_for_undoable_operations(string operation, bool expected)
    {
        var env = new TestEnvironment();
        Assert.Equal(expected, env.Parse(operation).TakesUndoSnapshot(env.Settings));
    }

    [Fact]
    public void Undo_restores_the_last_recorded_positions()
    {
        var env = new TestEnvironment();
        var parse = env.ParseContext;
        var undo = OperationParser.Parse("undo", parse);
        Assert.True(parse.UsesUndo);
        var window = env.FocusedWindow(new Rect(100, 100, 800, 600));
        UndoOperations.Record(env.Settings).Execute(env.Context);
        env.Parse("push up").Execute(env.Context);
        UndoOperations.Record(env.Settings).Execute(env.Context);
        env.Parse("push left").Execute(env.Context);

        undo.Execute(env.Context);
        Assert.Equal(new Point(100, 0), window.Frame.TopLeft);
        undo.Execute(env.Context);
        Assert.Equal(new Point(100, 100), window.Frame.TopLeft);
    }
}
