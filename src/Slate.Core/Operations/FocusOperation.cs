using Slate.Core.Configuration;
using Slate.Core.Geometry;
using Slate.Core.Text;
using Slate.Core.Windowing;

namespace Slate.Core.Operations;

/// <summary><c>focus direction|'app'</c>: focuses the nearest window in a direction, or an application's main window.</summary>
public sealed class FocusOperation : Operation
{
    private FocusOperation(Direction? direction, string? app)
    {
        Direction = direction;
        App = app;
    }

    public override OperationKind Kind => OperationKind.Focus;
    public Direction? Direction { get; }
    public string? App { get; }

    public static FocusOperation Parse(string text)
    {
        var tokens = StringTokenizer.Tokenize(text, 2);
        if (tokens.Count < 2)
            throw new ConfigException("Invalid Parameters", $"Invalid Parameters in '{text}'. Focus operations require the following format: 'focus direction'");
        string target = tokens[1];
        if (Directions.Parse(target) is { } direction) return new FocusOperation(direction, null);
        if (StringTokenizer.IsQuoted(target)) return new FocusOperation(null, StringTokenizer.RemoveQuotes(target));
        throw new ConfigException("Unable To Parse Binding", $"Unknown direction '{target}' in '{text}'");
    }

    public override bool Execute(OperationContext context) => ExecuteOn(context, null);

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        if (App is not null)
        {
            IApplication? app = context.Windows.Applications().FirstOrDefault(a => a.IsNamed(App));
            return app?.Activate(mainWindowOnly: true) ?? false;
        }
        IWindow? current = context.Windows.FocusedWindow();
        if (current is null || Direction is not { } direction) return false;
        IWindow? target = FindTarget(context, current, direction);
        return target?.Focus() ?? false;
    }

    /// <summary>
    /// Widens a probe strip next to the focused window by <c>focusCheckWidth</c> until <c>focusCheckWidthMax</c>,
    /// picking the window that overlaps it most, optionally preferring the focused window's application.
    /// </summary>
    public static IWindow? FindTarget(OperationContext context, IWindow current, Direction direction)
    {
        SlateSettings settings = context.Settings;
        int step = settings.GetInt(ConfigKeys.FocusCheckWidth);
        int max = settings.GetInt(ConfigKeys.FocusCheckWidthMax);
        bool preferSameApp = settings.GetBool(ConfigKeys.FocusPreferSameApp);
        Rect frame = current.Frame;
        int currentPid = current.Application.ProcessId;
        var candidates = context.Windows.Applications()
            .SelectMany(app => app.Windows)
            .Where(w => w.Id != current.Id && !w.IsMinimizedOrHidden && w.Title.Length > 0)
            .ToList();

        for (int width = step; width <= max && step > 0; width += step)
        {
            Rect probe = Probe(frame, direction, width);
            IWindow? best = null;
            double bestArea = 0;
            IWindow? bestSameApp = null;
            double bestSameAppArea = 0;
            foreach (IWindow candidate in candidates)
            {
                double area = probe.Intersect(candidate.Frame).Area;
                if (area <= 0) continue;
                if (area > bestArea)
                {
                    best = candidate;
                    bestArea = area;
                }
                if (candidate.Application.ProcessId == currentPid && area > bestSameAppArea)
                {
                    bestSameApp = candidate;
                    bestSameAppArea = area;
                }
            }
            if (preferSameApp && bestSameApp is not null) return bestSameApp;
            if (best is not null) return best;
        }
        return null;
    }

    private static Rect Probe(Rect frame, Direction direction, double width) => direction switch
    {
        Operations.Direction.Up => new Rect(frame.X, frame.Y - width, frame.Width, width),
        Operations.Direction.Down => new Rect(frame.X, frame.Bottom, frame.Width, width),
        Operations.Direction.Left => new Rect(frame.X - width, frame.Y, width, frame.Height),
        Operations.Direction.Right => new Rect(frame.Right, frame.Y, width, frame.Height),
        _ => frame,
    };
}
