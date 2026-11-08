using Slate.Core.Configuration;
using Slate.Core.Expressions;
using Slate.Core.Geometry;
using Slate.Core.Operations;
using Slate.Core.Screens;
using Slate.Core.Windowing;

namespace Slate.Core.Interaction;

/// <summary>Decides which windows get a hint, with which character, and where the hint is drawn.</summary>
public static class HintPlanner
{
    public const string DefaultCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public static List<HintPlacement> Plan(OperationContext context, string characters)
    {
        SlateSettings settings = context.Settings;
        ScreenLayout screens = context.Screens();
        var windows = context.Windows.Applications()
            .SelectMany(app => app.Windows)
            .Where(w => w.Title.Length > 0 && !w.IsMinimizedOrHidden)
            .ToList();
        var order = settings.GetEnum<WindowHintsOrder>(ConfigKeys.WindowHintsOrder);
        if (order == WindowHintsOrder.LeftToRight)
            windows = windows.OrderBy(w => w.Frame.X).ThenBy(w => w.Frame.Y).ToList();
        else if (order == WindowHintsOrder.RightToLeft)
            windows = windows.OrderByDescending(w => w.Frame.X).ThenBy(w => w.Frame.Y).ToList();
        if (order == WindowHintsOrder.Persist) context.HintNumbers.Retain(windows.Select(w => w.Id));

        var placements = new List<HintPlacement>();
        int nextCharacter = 0;
        foreach (IWindow window in windows)
        {
            int index = order == WindowHintsOrder.Persist ? context.HintNumbers.NumberFor(window.Id) : nextCharacter;
            if (index >= characters.Length)
            {
                if (order == WindowHintsOrder.Persist) continue;
                break;
            }
            Rect? bounds = HintBounds(context, screens, window, placements);
            if (bounds is null) continue;
            double scale = screens.Screens[ScreenOf(screens, window.Frame)].Scale;
            placements.Add(new HintPlacement(window, characters[index], bounds.Value, scale));
            nextCharacter++;
        }
        return placements;
    }

    /// <summary>
    /// Evaluates the candidate positions (<c>windowHintsTopLeftX/Y</c>) relative to the window and keeps the first visible
    /// one, then moves the hint down while it collides with an earlier hint when <c>windowHintsSpread</c> is set.
    /// </summary>
    public static Rect? HintBounds(OperationContext context, ScreenLayout screens, IWindow window, IReadOnlyList<HintPlacement> placed)
    {
        SlateSettings settings = context.Settings;
        Rect frame = window.Frame;
        int screenId = ScreenOf(screens, frame);
        if (screenId < 0) return null;

        var values = screens.ExpressionValues(screenId, frame, frame.Size);
        double scale = screens.Screens[screenId].Scale;
        double height = ExpressionEvaluator.Evaluate(settings.Get(ConfigKeys.WindowHintsHeight), values) * scale;
        double width = ExpressionEvaluator.Evaluate(settings.Get(ConfigKeys.WindowHintsWidth), values) * scale;
        values[ExpressionVariables.WindowHintsWidth] = width;
        values[ExpressionVariables.WindowHintsHeight] = height;

        var xs = settings.GetList(ConfigKeys.WindowHintsTopLeftX);
        var ys = settings.GetList(ConfigKeys.WindowHintsTopLeftY);
        bool ignoreHidden = settings.GetBool(ConfigKeys.WindowHintsIgnoreHiddenWindows);
        Rect? bounds = null;
        for (int i = 0; i < Math.Min(xs.Count, ys.Count); i++)
        {
            var candidate = new Rect(
                frame.X + ExpressionEvaluator.Evaluate(xs[i], values),
                frame.Y + ExpressionEvaluator.Evaluate(ys[i], values),
                width, height);
            if (!ignoreHidden || context.Windows.WindowAt(candidate.Center)?.Id == window.Id)
            {
                bounds = candidate;
                break;
            }
            if (!ignoreHidden) break;
        }
        if (bounds is not { } hint) return null;

        if (settings.GetBool(ConfigKeys.WindowHintsSpread))
        {
            double searchWidth = settings.GetDouble(ConfigKeys.WindowHintsSpreadSearchWidth);
            double searchHeight = settings.GetDouble(ConfigKeys.WindowHintsSpreadSearchHeight);
            double padding = settings.GetDouble(ConfigKeys.WindowHintsSpreadPadding);
            while (placed.Any(p => Collides(hint.TopLeft, p.Bounds.TopLeft, searchWidth, searchHeight)))
                hint = hint.Offset(0, height + padding);
        }
        return hint;
    }

    private static bool Collides(Point origin, Point other, double searchWidth, double searchHeight) =>
        new Rect(other.X - searchWidth / 2, other.Y - searchHeight / 2, searchWidth, searchHeight).Contains(origin);

    /// <summary>The screen holding the window's top-left corner, else its center, else another corner.</summary>
    private static int ScreenOf(ScreenLayout screens, Rect frame)
    {
        Point[] probes =
        [
            frame.TopLeft,
            frame.Center,
            new(frame.Right, frame.Top),
            new(frame.Right, frame.Bottom),
            new(frame.Left, frame.Bottom),
        ];
        foreach (Point p in probes)
        {
            int id = screens.ScreenIdForPoint(p);
            if (id >= 0) return id;
        }
        return -1;
    }
}
