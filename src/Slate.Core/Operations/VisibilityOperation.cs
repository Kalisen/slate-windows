using Slate.Core.Configuration;
using Slate.Core.Text;
using Slate.Core.Windowing;

namespace Slate.Core.Operations;

/// <summary>
/// <c>hide|show|toggle apps</c>. Windows has no application hiding: hiding minimizes every window of the application and
/// showing restores them.
/// </summary>
public sealed class VisibilityOperation(OperationKind kind, IReadOnlyList<string> apps) : Operation
{
    /// <summary>The focused application.</summary>
    public const string Current = "current";
    /// <summary>Every application.</summary>
    public const string All = "all";
    /// <summary>Every application except the quoted one (or <c>current</c>).</summary>
    public const string AllButPrefix = "all-but:";

    public override OperationKind Kind { get; } = kind;
    public IReadOnlyList<string> Apps { get; } = apps;

    public static VisibilityOperation Parse(OperationKind kind, string text)
    {
        var tokens = StringTokenizer.Tokenize(text, 2);
        if (tokens.Count < 2)
            throw new ConfigException("Invalid Parameters", $"Invalid Parameters in '{text}'. Visibility operations require the following format: 'hide|show|toggle apps'");
        var apps = tokens[1].Split(',').Select(app => StringTokenizer.RemoveQuotes(app.Trim())).ToList();
        return new VisibilityOperation(kind, apps);
    }

    public override bool Execute(OperationContext context) => ExecuteOn(context, null);

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        var running = context.Windows.Applications();
        IApplication? focused = context.Windows.FocusedWindow()?.Application;
        foreach (string app in Apps)
        {
            if (app == Current)
            {
                if (focused is not null) Apply(focused);
            }
            else if (app == All)
            {
                foreach (IApplication each in running) Apply(each);
            }
            else if (app.StartsWith(AllButPrefix, StringComparison.Ordinal))
            {
                string skip = StringTokenizer.RemoveQuotes(app[AllButPrefix.Length..]);
                int? skipPid = skip == Current ? focused?.ProcessId : null;
                foreach (IApplication each in running)
                {
                    if (skipPid is { } pid ? each.ProcessId == pid : each.IsNamed(skip)) continue;
                    Apply(each);
                }
            }
            else
            {
                foreach (IApplication each in running.Where(a => a.IsNamed(app))) Apply(each);
            }
        }
        return true;
    }

    private void Apply(IApplication app)
    {
        bool hide = Kind == OperationKind.Hide || (Kind == OperationKind.Toggle && !app.IsHidden);
        if (hide) app.Hide(); else app.Show();
    }
}
