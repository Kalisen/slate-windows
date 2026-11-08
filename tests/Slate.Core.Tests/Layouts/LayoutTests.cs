using Slate.Core.Configuration;
using Slate.Core.Geometry;
using Slate.Core.Layouts;
using Slate.Core.Operations;
using Slate.Core.Windowing;

namespace Slate.Core.Tests.Layouts;

public class LayoutTests
{
    private static Layout Parse(TestEnvironment env, params string[] lines)
    {
        var layout = new Layout("work");
        foreach (string line in lines) layout.Add(line, env.ParseContext);
        env.Layouts[layout.Name] = layout;
        return layout;
    }

    [Fact]
    public void Parses_application_options()
    {
        var options = ApplicationOptions.Parse("IGNORE_FAIL,REPEAT,MAIN_FIRST,SORT_TITLE,TITLE_ORDER=a;b,TITLE_ORDER_REGEX=^x;y$");
        Assert.True(options.IgnoreFail && options.Repeat && options.MainFirst && options.SortTitle);
        Assert.False(options.RepeatLast || options.MainLast);
        Assert.Equal(new[] { "a", "b" }, options.TitleOrder);
        Assert.Equal(new[] { "^x", "y$" }, options.TitleOrderRegex);
    }

    [Fact]
    public void Lines_with_the_same_name_build_one_layout()
    {
        var env = new TestEnvironment();
        var layout = Parse(env,
            "layout work 'Terminal' push up | push down",
            "layout work 'Google Chrome':REPEAT push left",
            "layout work BEFORE shell 'C:\\tools\\before.exe'",
            "layout work AFTER shell 'C:\\tools\\after.exe'");
        Assert.Equal(new[] { "Terminal", "Google Chrome" }, layout.AppOrder);
        Assert.Equal(2, layout.OperationsFor("Terminal").Count);
        Assert.True(layout.OptionsFor("Google Chrome").Repeat);
        Assert.Single(layout.BeforeOperations);
        Assert.Single(layout.AfterOperations);
    }

    [Fact]
    public void Activation_moves_each_window_and_runs_before_and_after()
    {
        var env = new TestEnvironment();
        var term = env.Windows.AddApp("Terminal");
        var first = term.AddWindow("one", new Rect(300, 300, 400, 400));
        var second = term.AddWindow("two", new Rect(300, 300, 400, 400));
        var third = term.AddWindow("three", new Rect(300, 300, 400, 400));
        Parse(env,
            "layout work 'Terminal' push up | push down",
            "layout work BEFORE shell 'C:\\tools\\before.exe'",
            "layout work AFTER shell 'C:\\tools\\after.exe'");

        Assert.True(env.Parse("layout work").Execute(env.Context));
        Assert.Equal(new Point(300, 0), first.Frame.TopLeft);
        Assert.Equal(new Point(300, 640), second.Frame.TopLeft);
        Assert.Equal(new Point(300, 300), third.Frame.TopLeft);
        Assert.Equal(new[] { @"C:\tools\before.exe", @"C:\tools\after.exe" }, env.Shell.Runs.Select(r => r.Command));
    }

    [Theory]
    [InlineData("MAIN_FIRST", new[] { "main", "c", "b" })]
    [InlineData("MAIN_LAST", new[] { "c", "b", "main" })]
    [InlineData("MAIN_FIRST,SORT_TITLE", new[] { "main", "b", "c" })]
    [InlineData("SORT_TITLE", new[] { "b", "c", "main" })]
    [InlineData("TITLE_ORDER=c;missing;b", new[] { "c", "b", "main" })]
    [InlineData("TITLE_ORDER_REGEX=^c$;^m", new[] { "c", "main", "b" })]
    [InlineData("MAIN_LAST,TITLE_ORDER=c", new[] { "c", "b", "main" })]
    public void Orders_windows(string options, string[] expected)
    {
        var app = new FakeApplication("App", 1);
        foreach (string title in new[] { "main", "c", "b" }) app.AddWindow(title, Rect.Empty);
        var ordered = LayoutActivator.OrderWindows(app.Windows, ApplicationOptions.Parse(options));
        Assert.Equal(expected, ordered.Select(w => w.Title));
    }

    [Theory]
    [InlineData("", false, new[] { 0, 1, -1, -1 })]
    [InlineData("REPEAT", false, new[] { 0, 1, 0, 1 })]
    [InlineData("REPEAT_LAST", false, new[] { 0, 1, 1, 1 })]
    [InlineData("", true, new[] { -1, 0, 1, -1 })]
    [InlineData("IGNORE_FAIL", true, new[] { -1, 1, -1, -1 })]
    public void Assigns_operations_to_windows(string options, bool firstFails, int[] expectedOperation)
    {
        var env = new TestEnvironment();
        var app = env.Windows.AddApp("App");
        var windows = Enumerable.Range(0, 4).Select(i => app.AddWindow($"w{i}", new Rect(500, 500, 100, 100))).ToList();
        windows[0].Fails = firstFails;
        var operations = new Operation[] { env.Parse("move 0;0 100;100"), env.Parse("move 1;1 100;100") };

        LayoutActivator.ApplyOperations(env.Context, windows, operations, ApplicationOptions.Parse(options), focus: false);

        int[] applied = windows.Select(w => w.Frame.X == 500 ? -1 : (int)w.Frame.X).ToArray();
        Assert.Equal(expectedOperation, applied);
    }

    [Fact]
    public void Focus_on_activate_follows_the_layout_order()
    {
        var env = new TestEnvironment();
        env.Settings.Set(ConfigKeys.LayoutFocusOnActivate, "true");
        var b = env.Windows.AddApp("B").AddWindow("b", new Rect(500, 500, 100, 100));
        var a = env.Windows.AddApp("A").AddWindow("a", new Rect(500, 500, 100, 100));
        Parse(env, "layout work 'A' push up", "layout work 'B' push up");
        env.Parse("layout work").Execute(env.Context);
        Assert.Equal(b, env.Windows.Focused);
        Assert.True(a.Focused);
    }

    [Fact]
    public void Invalid_layout_lines_throw() =>
        Assert.Throws<ConfigException>(() => Parse(new TestEnvironment(), "layout work 'App'"));
}
