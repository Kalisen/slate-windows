using System.Globalization;
using Slate.Core.Expressions;
using Slate.Core.Geometry;

namespace Slate.Core.Screens;

/// <summary>
/// A monitor. <see cref="Id"/> is the system index; the primary screen is always 0. <see cref="Scale"/> is the display
/// scale factor (1.0 at 96 DPI), applied to overlay sizes.
/// </summary>
public sealed record ScreenInfo(int Id, Rect Frame, Rect VisibleFrame, double Scale = 1.0);

public interface IScreenProvider
{
    /// <summary>Connected screens, primary first.</summary>
    IReadOnlyList<ScreenInfo> Screens();
}

/// <summary>Special screen ids returned by screen resolution.</summary>
public static class ScreenIds
{
    public const int Main = 0;
    public const int Current = -1;
    /// <summary>The referenced screen does not exist: the operation leaves the window where it is.</summary>
    public const int Ignore = -2;
}

/// <summary>Words that reference a screen relative to the current one.</summary>
public static class ScreenReferences
{
    public const string Current = "-1";
    public const string Left = "left";
    public const string Right = "right";
    public const string Up = "up";
    public const string Above = "above";
    public const string Down = "down";
    public const string Below = "below";
    public const string Next = "next";
    public const string Previous = "previous";
    public const string Prev = "prev";
    public const string OrderedPrefix = "ordered:";
    public const char ResolutionSeparator = 'x';
}

/// <summary>
/// A snapshot of the connected screens with Slate's screen numbering. Screen references in configs use the
/// left-to-right order unless <c>orderScreensLeftToRight</c> is false, in which case they use system ids.
/// </summary>
public sealed class ScreenLayout
{
    private readonly IReadOnlyList<ScreenInfo> screens;
    private readonly int[] leftToRightToSystem;
    private readonly bool orderLeftToRight;
    private readonly bool defaultToCurrentScreen;

    public ScreenLayout(IReadOnlyList<ScreenInfo> screens, bool orderLeftToRight, bool defaultToCurrentScreen)
    {
        this.screens = screens;
        this.orderLeftToRight = orderLeftToRight;
        this.defaultToCurrentScreen = defaultToCurrentScreen;
        leftToRightToSystem = ComputeLeftToRight(screens);
    }

    public int Count => screens.Count;

    public IReadOnlyList<ScreenInfo> Screens => screens;

    /// <summary>System ids sorted left to right (top to bottom for screens sharing an x origin, lowest first).</summary>
    public IReadOnlyList<int> LeftToRightOrder => leftToRightToSystem;

    public static int[] ComputeLeftToRight(IReadOnlyList<ScreenInfo> screens) =>
        screens.Select((s, i) => (s.Frame, i))
            .OrderBy(t => t.Frame.X)
            .ThenByDescending(t => t.Frame.Y)
            .Select(t => t.i)
            .ToArray();

    /// <summary>Screen resolutions <c>WIDTHxHEIGHT</c> in system order.</summary>
    public IReadOnlyList<string> ResolutionStrings() =>
        screens.Select(s => ResolutionString(s.Frame.Size)).ToList();

    public static string ResolutionString(Size size) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)size.Width}{ScreenReferences.ResolutionSeparator}{(int)size.Height}");

    public bool Exists(int screenId) => screenId >= ScreenIds.Main && screenId < screens.Count;

    public Rect Frame(int screenId) => Exists(screenId) ? screens[screenId].Frame : Rect.Empty;

    public Rect VisibleFrame(int screenId) => Exists(screenId) ? screens[screenId].VisibleFrame : Rect.Empty;

    public int LeftToRightIndexOf(int screenId) => Array.IndexOf(leftToRightToSystem, screenId);

    /// <summary>The id users see for <paramref name="screenId"/> (left-to-right index when that ordering is on).</summary>
    public int ToReferenceId(int screenId) => orderLeftToRight ? LeftToRightIndexOf(screenId) : screenId;

    /// <summary>The system id of a screen given the id used in configs.</summary>
    public int FromReferenceId(int referenceId) =>
        orderLeftToRight && referenceId >= 0 && referenceId < leftToRightToSystem.Length ? leftToRightToSystem[referenceId] : referenceId;

    /// <summary>The screen with the largest overlap with <paramref name="rect"/>, or <see cref="ScreenIds.Ignore"/>.</summary>
    public int ScreenIdForRect(Rect rect)
    {
        double largest = 0;
        int result = ScreenIds.Ignore;
        for (int i = 0; i < screens.Count; i++)
        {
            double area = screens[i].Frame.Intersect(rect).Area;
            if (area > largest)
            {
                largest = area;
                result = i;
            }
        }
        return result;
    }

    /// <summary>The first screen containing <paramref name="point"/>, or -1.</summary>
    public int ScreenIdForPoint(Point point)
    {
        for (int i = 0; i < screens.Count; i++)
        {
            if (screens[i].Frame.Contains(point)) return i;
        }
        return -1;
    }

    /// <summary>
    /// Resolves a screen reference (index, <c>ordered:N</c>, <c>WxH</c>, direction, next/previous) relative to the
    /// screen of <paramref name="window"/>. Returns a system id or <see cref="ScreenIds.Ignore"/>.
    /// </summary>
    public int Resolve(string screenRef, Rect window)
    {
        int current = ScreenIdForRect(window);
        Rect currentFrame = Frame(current);
        string reference = screenRef.Trim().ToLowerInvariant();
        int screenId;
        switch (reference)
        {
            case ScreenReferences.Right:
                screenId = ScreenIdForRect(new Rect(currentFrame.Right, currentFrame.Y, 1, currentFrame.Height));
                break;
            case ScreenReferences.Left:
                screenId = ScreenIdForRect(new Rect(currentFrame.X - 1, currentFrame.Y, 1, currentFrame.Height));
                break;
            case ScreenReferences.Up or ScreenReferences.Above:
                screenId = ScreenIdForRect(new Rect(currentFrame.X, currentFrame.Y - 1, currentFrame.Width, 1));
                break;
            case ScreenReferences.Down or ScreenReferences.Below:
                screenId = ScreenIdForRect(new Rect(currentFrame.X, currentFrame.Bottom, currentFrame.Width, 1));
                break;
            case ScreenReferences.Next:
                screenId = current == screens.Count - 1 ? ScreenIds.Main : current + 1;
                break;
            case ScreenReferences.Previous or ScreenReferences.Prev:
                screenId = current == ScreenIds.Main ? screens.Count - 1 : current - 1;
                break;
            default:
                screenId = ResolveExplicit(reference);
                break;
        }
        return Normalize(screenId, current);
    }

    private int ResolveExplicit(string reference)
    {
        if (reference.StartsWith(ScreenReferences.OrderedPrefix, StringComparison.Ordinal))
        {
            if (!int.TryParse(reference[ScreenReferences.OrderedPrefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ordered))
                return ScreenIds.Ignore;
            return ordered >= 0 && ordered < leftToRightToSystem.Length ? leftToRightToSystem[ordered] : ordered;
        }
        if (reference.Contains(ScreenReferences.ResolutionSeparator, StringComparison.Ordinal))
        {
            string[] parts = reference.Split(ScreenReferences.ResolutionSeparator);
            if (parts.Length < 2 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
                return ScreenIds.Ignore;
            for (int i = 0; i < screens.Count; i++)
            {
                if ((int)screens[i].Frame.Width == width && (int)screens[i].Frame.Height == height) return i;
            }
            return defaultToCurrentScreen ? ScreenIds.Current : ScreenIds.Ignore;
        }
        if (!int.TryParse(reference, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)) return ScreenIds.Ignore;
        return index < ScreenIds.Main || index >= screens.Count ? index : FromReferenceId(index);
    }

    private int Normalize(int screenId, int current)
    {
        if (screenId == ScreenIds.Current) return current;
        if (screenId < ScreenIds.Main) return ScreenIds.Ignore;
        if (screenId < screens.Count) return screenId;
        return defaultToCurrentScreen ? current : ScreenIds.Ignore;
    }

    /// <summary>The variables available to expressions for a window on <paramref name="screenId"/>.</summary>
    public Dictionary<string, double> ExpressionValues(int screenId, Rect window, Size newSize)
    {
        Rect screen = VisibleFrame(screenId);
        return new Dictionary<string, double>
        {
            [ExpressionVariables.ScreenOriginX] = screen.X,
            [ExpressionVariables.ScreenOriginY] = screen.Y,
            [ExpressionVariables.ScreenSizeX] = screen.Width,
            [ExpressionVariables.ScreenSizeY] = screen.Height,
            [ExpressionVariables.WindowSizeX] = window.Width,
            [ExpressionVariables.WindowSizeY] = window.Height,
            [ExpressionVariables.NewWindowSizeX] = newSize.Width,
            [ExpressionVariables.NewWindowSizeY] = newSize.Height,
            [ExpressionVariables.WindowTopLeftX] = window.X,
            [ExpressionVariables.WindowTopLeftY] = window.Y,
        };
    }

    /// <summary>
    /// A comparable fingerprint of the screen setup (resolutions and left-to-right order), used to detect changes.
    /// </summary>
    public string Fingerprint() =>
        string.Join(",", ResolutionStrings()) + "|" + string.Join(",", leftToRightToSystem);
}
