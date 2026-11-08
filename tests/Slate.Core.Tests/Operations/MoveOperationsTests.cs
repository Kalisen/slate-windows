using Slate.Core.Configuration;
using Slate.Core.Geometry;

namespace Slate.Core.Tests.Operations;

/// <summary>Window moves on a 1920x1080 primary screen (visible 1920x1040) and a 1280x1024 screen on its right.</summary>
public class MoveOperationsTests
{
    private static readonly Rect Start = new(100, 100, 800, 600);

    private static Rect Run(string operation, Rect? start = null, Action<TestEnvironment>? setup = null)
    {
        var env = new TestEnvironment();
        env.Screens.List.Add(FakeScreens.Right);
        setup?.Invoke(env);
        var window = env.FocusedWindow(start ?? Start);
        Assert.True(env.Parse(operation).Execute(env.Context));
        return window.Frame;
    }

    [Theory]
    [InlineData("move 0;0 100;100", 0, 0, 100, 100)]
    [InlineData("move screenOriginX;screenOriginY screenSizeX/2;screenSizeY 1", 1920, 0, 640, 1024)]
    [InlineData("move 10,20 30,40", 10, 20, 30, 40)]
    [InlineData("move screenSizeX-newWindowSizeX;0 300;200", 1620, 0, 300, 200)]
    public void Move(string operation, double x, double y, double w, double h) =>
        Assert.Equal(new Rect(x, y, w, h), Run(operation));

    [Theory]
    [InlineData("push up", 100, 0, 800, 600)]
    [InlineData("push up center", 560, 0, 800, 600)]
    [InlineData("push up bar", 0, 0, 1920, 600)]
    [InlineData("push top bar-resize:screenSizeY/2", 0, 0, 1920, 520)]
    [InlineData("push down", 100, 440, 800, 600)]
    [InlineData("push bottom center", 560, 440, 800, 600)]
    [InlineData("push down bar", 0, 440, 1920, 600)]
    [InlineData("push down bar-resize:screenSizeY/4", 0, 780, 1920, 260)]
    [InlineData("push down bar-resize:screenSizeY/2-20", 0, 540, 1920, 500)]
    [InlineData("push left", 0, 100, 800, 600)]
    [InlineData("push left center", 0, 220, 800, 600)]
    [InlineData("push left bar", 0, 0, 800, 1040)]
    [InlineData("push left bar-resize:screenSizeX/3", 0, 0, 640, 1040)]
    [InlineData("push left bar-resize:min({screenSizeX/2,800})", 0, 0, 800, 1040)]
    [InlineData("push right", 1120, 100, 800, 600)]
    [InlineData("push right center", 1120, 220, 800, 600)]
    [InlineData("push right bar", 1120, 0, 800, 1040)]
    [InlineData("push right bar-resize:screenSizeX/3", 1280, 0, 640, 1040)]
    [InlineData("push right bar-resize:screenSizeX/2+20", 940, 0, 980, 1040)]
    [InlineData("push right none 1", 2400, 100, 800, 600)]
    public void Push(string operation, double x, double y, double w, double h) =>
        Assert.Equal(new Rect(x, y, w, h), Run(operation));

    [Theory]
    [InlineData("corner top-left", 0, 0, 800, 600)]
    [InlineData("corner top-right", 1120, 0, 800, 600)]
    [InlineData("corner bottom-left", 0, 440, 800, 600)]
    [InlineData("corner bottom-right resize:screenSizeX/2;screenSizeY/2", 960, 520, 960, 520)]
    [InlineData("corner bottom-right resize:screenSizeX/2-20;screenSizeY/2+20", 980, 500, 940, 540)]
    [InlineData("corner top-right resize:screenSizeX/2-20;screenSizeY", 980, 0, 940, 1040)]
    [InlineData("corner bottom-left resize:screenSizeX;screenSizeY/2-20", 0, 540, 1920, 500)]
    [InlineData("corner top-left resize:screenSizeX/2;screenSizeY/2 1", 1920, 0, 640, 512)]
    public void Corner(string operation, double x, double y, double w, double h) =>
        Assert.Equal(new Rect(x, y, w, h), Run(operation));

    [Theory]
    [InlineData("throw 1", 1920, 0, 800, 600)]
    [InlineData("throw 1 noresize", 1920, 0, 800, 600)]
    [InlineData("throw 1 resize", 1920, 0, 1280, 1024)]
    [InlineData("throw right resize:screenSizeX/2;screenSizeY", 1920, 0, 640, 1024)]
    [InlineData("throw next", 1920, 0, 800, 600)]
    public void Throw(string operation, double x, double y, double w, double h) =>
        Assert.Equal(new Rect(x, y, w, h), Run(operation));

    [Fact]
    public void Throw_to_a_missing_screen_leaves_the_window_alone() => Assert.Equal(Start, Run("throw 5 resize"));

    [Theory]
    [InlineData("nudge +10% +0", "screenSize", 292, 100)]
    [InlineData("nudge -10% +10%", "screenSize", -92, 204)]
    [InlineData("nudge +10% +0", "windowSize", 180, 100)]
    [InlineData("nudge -100 +50", "screenSize", 0, 150)]
    [InlineData("nudge 100 0", "screenSize", 200, 100)]
    public void Nudge(string operation, string percentOf, double x, double y) =>
        Assert.Equal(new Rect(x, y, 800, 600), Run(operation, setup: env => env.Settings.Set(ConfigKeys.NudgePercentOf, percentOf)));

    [Fact]
    public void Move_positions_with_the_size_the_window_accepted()
    {
        var env = new TestEnvironment();
        var window = env.FocusedWindow(Start);
        window.MinimumSize = new Size(500, 500);
        env.Parse("push right bar-resize:100").Execute(env.Context);
        Assert.Equal(new Rect(1820, 0, 500, 1040), window.Frame);
    }

    [Fact]
    public void Window_operations_without_a_focused_window_fail()
    {
        var env = new TestEnvironment();
        Assert.False(env.Parse("push up").Execute(env.Context));
    }

    [Theory]
    [InlineData("move 0;0", "Invalid Parameters")]
    [InlineData("move 0 0;0", "Invalid Parameters")]
    [InlineData("move 0;0 unknown;1", "Unable To Parse Binding")]
    [InlineData("push", "Invalid Parameters")]
    [InlineData("push sideways", "Unrecognized Direction")]
    [InlineData("push behind", "Unrecognized Direction")]
    [InlineData("push up diagonal", "Unrecognized Style")]
    [InlineData("corner middle", "Unrecognized Corner")]
    [InlineData("throw", "Invalid Parameters")]
    [InlineData("throw 1 stretch", "Unrecognized Style")]
    [InlineData("nudge +10%", "Invalid Parameters")]
    public void Invalid_operations_are_rejected_when_parsed(string operation, string title) =>
        Assert.Equal(title, Assert.Throws<ConfigException>(() => new TestEnvironment().Parse(operation)).Title);
}
