using System.Text.RegularExpressions;
using Slate.Core.Configuration;
using Slate.Core.Operations;
using Slate.Core.Windowing;

namespace Slate.Core.Layouts;

/// <summary>Applies a <see cref="Layout"/> to the running applications.</summary>
public static class LayoutActivator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    public static bool Activate(OperationContext context, Layout layout)
    {
        bool focusOnActivate = context.Settings.GetBool(ConfigKeys.LayoutFocusOnActivate);
        var running = context.Windows.Applications();
        IEnumerable<IApplication> apps = focusOnActivate
            ? layout.AppOrder.Select(name => running.FirstOrDefault(a => a.IsNamed(name))).OfType<IApplication>()
            : running;

        foreach (Operation op in layout.BeforeOperations) op.Execute(context);

        bool success = true;
        foreach (IApplication app in apps)
        {
            string? layoutName = layout.AppOrder.FirstOrDefault(app.IsNamed);
            if (layoutName is null || app.Windows.Count == 0) continue;
            var options = layout.OptionsFor(layoutName);
            var windows = OrderWindows(app.Windows, options);
            success = ApplyOperations(context, windows, layout.OperationsFor(layoutName), options, focusOnActivate) && success;
        }

        foreach (Operation op in layout.AfterOperations) op.Execute(context);
        return success;
    }

    /// <summary>
    /// Orders windows: the main window first/last (MAIN_FIRST/MAIN_LAST), then TITLE_ORDER and TITLE_ORDER_REGEX matches,
    /// then the rest (sorted by title with SORT_TITLE). The first window of <paramref name="windows"/> is the main one.
    /// </summary>
    public static List<IWindow> OrderWindows(IReadOnlyList<IWindow> windows, ApplicationOptions options)
    {
        var remaining = windows.ToList();
        var ordered = new List<IWindow>();
        var appended = new List<IWindow>();
        if ((options.MainFirst || options.MainLast) && remaining.Count > 0)
        {
            (options.MainFirst ? ordered : appended).Add(remaining[0]);
            remaining.RemoveAt(0);
        }
        foreach (string title in options.TitleOrder ?? [])
        {
            IWindow? match = remaining.FirstOrDefault(w => w.Title == title);
            if (match is null) continue;
            ordered.Add(match);
            remaining.Remove(match);
        }
        foreach (string pattern in options.TitleOrderRegex ?? [])
        {
            Regex regex;
            try { regex = new Regex(pattern, RegexOptions.None, RegexTimeout); }
            catch (ArgumentException) { continue; }
            IWindow? match = remaining.FirstOrDefault(w => regex.IsMatch(w.Title));
            if (match is null) continue;
            ordered.Add(match);
            remaining.Remove(match);
        }
        ordered.AddRange(options.SortTitle ? remaining.OrderBy(w => w.Title, StringComparer.Ordinal) : remaining);
        ordered.AddRange(appended);
        return ordered;
    }

    /// <summary>
    /// Runs one operation per window. Without IGNORE_FAIL a failed window does not consume its operation, so the next window
    /// gets it. REPEAT cycles through the operations, REPEAT_LAST reuses the last one, otherwise extra windows are left alone.
    /// </summary>
    public static bool ApplyOperations(OperationContext context, IReadOnlyList<IWindow> windows, IReadOnlyList<Operation> operations,
        ApplicationOptions options, bool focus)
    {
        bool success = true;
        int failed = 0;
        for (int i = 0; i < windows.Count; i++)
        {
            int index = i - failed;
            if (options.Repeat) index %= operations.Count;
            else if (options.RepeatLast) index = Math.Min(index, operations.Count - 1);
            else if (index >= operations.Count) break;

            bool windowSuccess = operations[index].ExecuteOn(context, windows[i]);
            success = windowSuccess && success;
            if (focus) windows[i].Focus();
            if (!options.IgnoreFail && !windowSuccess) failed++;
        }
        return success;
    }
}
