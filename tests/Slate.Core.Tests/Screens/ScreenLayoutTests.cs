using Slate.Core.Expressions;
using Slate.Core.Geometry;
using Slate.Core.Screens;

namespace Slate.Core.Tests.Screens;

public class ScreenLayoutTests
{
    // System order: primary (middle), a screen on the left, a screen on the right, a screen below the primary.
    private static readonly ScreenInfo[] Screens =
    [
        new(0, new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1040)),
        new(1, new Rect(-1280, 0, 1280, 1024), new Rect(-1280, 0, 1280, 1024)),
        new(2, new Rect(1920, 0, 2560, 1440), new Rect(1920, 0, 2560, 1440)),
        new(3, new Rect(0, 1080, 1920, 1080), new Rect(0, 1080, 1920, 1080)),
    ];

    private static readonly Rect OnPrimary = new(100, 100, 800, 600);

    private static ScreenLayout Layout(bool leftToRight = true, bool defaultToCurrent = false) => new(Screens, leftToRight, defaultToCurrent);

    [Fact]
    public void Orders_screens_left_to_right_lowest_first_on_ties() =>
        Assert.Equal(new[] { 1, 3, 0, 2 }, Layout().LeftToRightOrder);

    [Theory]
    [InlineData("0", true, 1)]
    [InlineData("2", true, 0)]
    [InlineData("2", false, 2)]
    [InlineData("ordered:3", false, 2)]
    [InlineData("2560x1440", true, 2)]
    [InlineData("right", true, 2)]
    [InlineData("left", true, 1)]
    [InlineData("down", true, 3)]
    [InlineData("below", true, 3)]
    [InlineData("next", true, 1)]
    [InlineData("previous", true, 3)]
    [InlineData("prev", true, 3)]
    [InlineData("-1", true, 0)]
    public void Resolves_screen_references(string reference, bool leftToRight, int expected) =>
        Assert.Equal(expected, Layout(leftToRight).Resolve(reference, OnPrimary));

    [Theory]
    [InlineData("9", false, ScreenIds.Ignore)]
    [InlineData("9", true, 0)]
    [InlineData("800x600", false, ScreenIds.Ignore)]
    [InlineData("800x600", true, 0)]
    [InlineData("up", false, ScreenIds.Ignore)]
    [InlineData("ordered:4", false, ScreenIds.Ignore)]
    public void Missing_screens_are_ignored_or_fall_back_to_the_current_one(string reference, bool defaultToCurrent, int expected) =>
        Assert.Equal(expected, Layout(defaultToCurrent: defaultToCurrent).Resolve(reference, OnPrimary));

    [Fact]
    public void Screen_for_rect_is_the_largest_overlap()
    {
        var mostlyRight = new Rect(1800, 100, 400, 300);
        Assert.Equal(2, Layout().ScreenIdForRect(mostlyRight));
        Assert.Equal(ScreenIds.Ignore, Layout().ScreenIdForRect(new Rect(-5000, -5000, 10, 10)));
    }

    [Fact]
    public void Expression_values_use_the_visible_frame_and_window()
    {
        var values = Layout().ExpressionValues(0, OnPrimary, new Size(50, 60));
        Assert.Equal(1040, values[ExpressionVariables.ScreenSizeY]);
        Assert.Equal(100, values[ExpressionVariables.WindowTopLeftX]);
        Assert.Equal(800, values[ExpressionVariables.WindowSizeX]);
        Assert.Equal(60, values[ExpressionVariables.NewWindowSizeY]);
    }

    [Fact]
    public void Fingerprint_changes_with_resolutions()
    {
        var other = new ScreenLayout([Screens[0] with { Frame = new Rect(0, 0, 2560, 1440) }], true, false);
        Assert.NotEqual(Layout().Fingerprint(), other.Fingerprint());
        Assert.Equal(Layout().Fingerprint(), Layout().Fingerprint());
    }

    [Theory]
    [InlineData("default work count:4", true)]
    [InlineData("default work count:2", false)]
    [InlineData("default work resolutions:2560x1440;1920x1080;1280x1024;1920x1080", true)]
    [InlineData("default work resolutions:1920x1080", false)]
    public void Default_layouts_match_screen_setups(string line, bool matches) =>
        Assert.Equal(matches, DefaultLayout.Parse(line).Matches(Layout()));

    [Theory]
    [InlineData("default work")]
    [InlineData("default work count")]
    [InlineData("default work size:2")]
    public void Invalid_default_directives_throw(string line) =>
        Assert.Throws<Slate.Core.Configuration.ConfigException>(() => DefaultLayout.Parse(line));
}
