using Slate.Core.Configuration;
using Slate.Core.Text;

namespace Slate.Core.Operations;

/// <summary>Parses an operation string such as <c>push up bar-resize:screenSizeY/2</c>.</summary>
public static class OperationParser
{
    public static Operation Parse(string text, ParseContext context)
    {
        text = text.Trim();
        string name = StringTokenizer.FirstToken(text);
        if (!OperationKinds.TryParse(name, out OperationKind kind))
            throw new ConfigException("Unrecognized Operation", $"Unrecognized operation '{name}' in '{text}'");

        return kind switch
        {
            OperationKind.Move => MoveOperations.ParseMove(text),
            OperationKind.Resize => ResizeOperation.Parse(text),
            OperationKind.Push => MoveOperations.ParsePush(text),
            OperationKind.Nudge => MoveOperations.ParseNudge(text, context.Settings),
            OperationKind.Throw => MoveOperations.ParseThrow(text),
            OperationKind.Corner => MoveOperations.ParseCorner(text),
            OperationKind.Chain => ChainOperation.Parse(text, context),
            OperationKind.Layout => LayoutOperation.Parse(text, context),
            OperationKind.Focus => FocusOperation.Parse(text),
            OperationKind.Snapshot => SnapshotOperation.Parse(text),
            OperationKind.ActivateSnapshot => ActivateSnapshotOperation.Parse(text),
            OperationKind.DeleteSnapshot => DeleteSnapshotOperation.Parse(text),
            OperationKind.Hint => HintOperation.Parse(text),
            OperationKind.Switch => SwitchOperation.Parse(text, context),
            OperationKind.Grid => GridOperation.Parse(text),
            OperationKind.Sequence => SequenceOperation.Parse(text, context),
            OperationKind.Hide or OperationKind.Show or OperationKind.Toggle => VisibilityOperation.Parse(kind, text),
            OperationKind.Relaunch => new RelaunchOperation(),
            OperationKind.Shell => ShellOperation.Parse(text, context),
            OperationKind.Undo => ParseUndo(context),
            _ => throw new ConfigException("Unrecognized Operation", $"Unrecognized operation '{name}' in '{text}'"),
        };
    }

    private static Operation ParseUndo(ParseContext context)
    {
        context.UsesUndo = true;
        return UndoOperations.Undo();
    }
}
