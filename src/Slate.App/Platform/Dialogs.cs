using Slate.Core.Configuration;

namespace Slate.App.Platform;

/// <summary>Task dialogs replacing the NSAlert prompts of the macOS version.</summary>
internal static class Dialogs
{
    private const int MaxListedLines = 20;

    /// <summary>Shows a warning with Quit/Skip (or Continue) buttons; returns true when the user chose Quit.</summary>
    public static bool WarnAndAskQuit(string title, string message, string continueLabel = "Skip")
    {
        var quit = new TaskDialogButton("Quit Slate");
        var proceed = new TaskDialogButton(continueLabel);
        var page = new TaskDialogPage
        {
            Caption = AppPaths.AppName,
            Heading = title,
            Text = message,
            Icon = TaskDialogIcon.Warning,
            Buttons = { proceed, quit },
            DefaultButton = proceed,
        };
        return TaskDialog.ShowDialog(page, TaskDialogStartupLocation.CenterScreen) == quit;
    }

    public static void Info(string title, string message)
    {
        var page = new TaskDialogPage
        {
            Caption = AppPaths.AppName,
            Heading = title,
            Text = message,
            Icon = TaskDialogIcon.Information,
            Buttons = { TaskDialogButton.OK },
        };
        TaskDialog.ShowDialog(page, TaskDialogStartupLocation.CenterScreen);
    }

    public static string List(IReadOnlyList<string> items)
    {
        if (items.Count == 0) return "  (none)";
        var shown = items.Take(MaxListedLines).Select(item => "  " + AbbreviateHome(item)).ToList();
        if (items.Count > MaxListedLines) shown.Add($"  ...and {items.Count - MaxListedLines} more");
        return string.Join(Environment.NewLine, shown);
    }

    private static string AbbreviateHome(string path) =>
        path.StartsWith(AppPaths.HomeDirectory, StringComparison.OrdinalIgnoreCase) ? "~" + path[AppPaths.HomeDirectory.Length..] : path;
}

internal sealed class DialogConfigErrorReporter : IConfigErrorReporter
{
    public bool ReportAndAskQuit(ConfigException error)
    {
        Log.Error($"{error.Title}: {error.Message}");
        return Dialogs.WarnAndAskQuit(error.Title, error.Message,
            error.Title.StartsWith("Could not load", StringComparison.Ordinal) ? "Continue" : "Skip");
    }
}

/// <summary>Asks the user to approve a changed configuration, listing its files and shell commands.</summary>
internal sealed class DialogConfigApprover : IConfigApprover
{
    public bool Approve(ConfigReview review)
    {
        var load = new TaskDialogButton("Load Config");
        var skip = new TaskDialogButton("Don't Load");
        var page = new TaskDialogPage
        {
            Caption = AppPaths.AppName,
            Heading = "Slate's configuration changed",
            Text = "Approve only if you made these changes. Slate runs your config with control over your windows and keyboard." +
                   $"{Environment.NewLine}{Environment.NewLine}Files:{Environment.NewLine}{Dialogs.List(review.Paths)}" +
                   $"{Environment.NewLine}{Environment.NewLine}Shell commands:{Environment.NewLine}{Dialogs.List(review.ShellLines)}",
            Icon = TaskDialogIcon.Warning,
            Buttons = { load, skip },
            DefaultButton = skip,
        };
        bool approved = TaskDialog.ShowDialog(page, TaskDialogStartupLocation.CenterScreen) == load;
        Log.Info(approved ? "Config approved by user." : "Config was not approved. Running without bindings or layouts.");
        return approved;
    }
}
