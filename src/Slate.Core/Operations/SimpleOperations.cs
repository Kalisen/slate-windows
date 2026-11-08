using Slate.Core.Configuration;
using Slate.Core.Layouts;
using Slate.Core.Snapshots;
using Slate.Core.Text;
using Slate.Core.Windowing;

namespace Slate.Core.Operations;

/// <summary>
/// <c>shell [wait] [path:dir] 'command args'</c>: starts a program. The command must be an absolute path; quote it with
/// double quotes inside the single-quoted string when it contains spaces.
/// </summary>
public sealed class ShellOperation(string command, IReadOnlyList<string> arguments, bool wait, string? workingDirectory) : Operation
{
    public const string WaitOption = "wait";
    public const string PathOptionPrefix = "path:";
    private const string CommandQuote = "\"";

    public override OperationKind Kind => OperationKind.Shell;
    public string Command { get; } = command;
    public IReadOnlyList<string> Arguments { get; } = arguments;
    public bool Wait { get; } = wait;
    public string? WorkingDirectory { get; } = workingDirectory;

    public static ShellOperation Parse(string text, ParseContext context)
    {
        var tokens = StringTokenizer.Tokenize(text, quoteChars: StringTokenizer.Quotes);
        if (tokens.Count < 2)
            throw new ConfigException("Invalid Parameters", $"Invalid Parameters in '{text}'. Shell operations require the following format: shell [wait] 'command'");
        bool wait = false;
        string? path = null;
        foreach (string option in tokens.Skip(1).SkipLast(1))
        {
            if (option == WaitOption) wait = true;
            else if (option.StartsWith(PathOptionPrefix, StringComparison.Ordinal)) path = context.ExpandHome(option[PathOptionPrefix.Length..]);
        }
        var commandAndArgs = StringTokenizer.Tokenize(tokens[^1], quoteChars: CommandQuote);
        if (commandAndArgs.Count < 1)
            throw new ConfigException("Invalid Parameters", $"Invalid Parameters in '{text}'. Shell operations require the following format: shell [wait] 'command'");
        return new ShellOperation(AbsoluteCommand(commandAndArgs[0], context), commandAndArgs.Skip(1).ToList(), wait, path);
    }

    /// <summary>Expands <c>~</c> and rejects commands that are not fully qualified paths.</summary>
    public static string AbsoluteCommand(string command, ParseContext context)
    {
        string expanded = context.ExpandHome(command);
        if (!Path.IsPathFullyQualified(expanded))
            throw new ConfigException("Invalid Shell Command",
                $"Shell command '{command}' must be an absolute path, e.g. 'C:\\Windows\\System32\\notepad.exe'.");
        return expanded;
    }

    public override bool Execute(OperationContext context) => ExecuteOn(context, null);

    public override bool ExecuteOn(OperationContext context, IWindow? window) =>
        context.Shell.Run(Command, Arguments, Wait, WorkingDirectory);
}

/// <summary><c>relaunch</c>: restarts Slate, reloading the configuration.</summary>
public sealed class RelaunchOperation : Operation
{
    public override OperationKind Kind => OperationKind.Relaunch;

    public override bool Execute(OperationContext context) => ExecuteOn(context, null);

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        context.App.Relaunch();
        return true;
    }
}

/// <summary><c>layout name</c>: activates a layout defined earlier in the config.</summary>
public sealed class LayoutOperation(string name) : Operation
{
    public override OperationKind Kind => OperationKind.Layout;
    public string Name { get; } = name;

    public static LayoutOperation Parse(string text, ParseContext context)
    {
        var tokens = StringTokenizer.Tokenize(text, 2);
        if (tokens.Count < 2)
            throw new ConfigException("Invalid Parameters", $"Invalid Parameters in '{text}'. Layout operations require the following format: 'layout <name>'");
        if (!context.Layouts.ContainsKey(tokens[1]))
            throw new ConfigException("Unrecognized Layout", $"Layout '{tokens[1]}' must be defined before it is used.");
        return new LayoutOperation(tokens[1]);
    }

    public override bool Execute(OperationContext context) => ExecuteOn(context, null);

    public override bool ExecuteOn(OperationContext context, IWindow? window) =>
        context.Layouts.TryGetValue(Name, out Layout? layout)
            ? LayoutActivator.Activate(context, layout)
            : throw new ConfigException("Unrecognized Layout", Name);
}

/// <summary>Builds the operations behind <c>undo</c>: a snapshot stack recorded before undoable operations.</summary>
public static class UndoOperations
{
    /// <summary><c>undo</c>: restores and pops the last undo snapshot.</summary>
    public static ActivateSnapshotOperation Undo() => new(SnapshotNames.Undo, delete: true, OperationKind.Undo);

    public static SnapshotOperation Record(SlateSettings settings) =>
        new(SnapshotNames.Undo, saveToDisk: true, isStack: true, settings.GetInt(ConfigKeys.UndoMaxStackSize));

    public static DeleteSnapshotOperation Clear() => new(SnapshotNames.Undo, all: true);
}
