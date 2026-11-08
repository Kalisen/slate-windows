using Slate.Core.Windowing;

namespace Slate.Core.Operations;

/// <summary>Something a binding runs. Window operations act on the focused window unless given one.</summary>
public abstract class Operation
{
    public abstract OperationKind Kind { get; }

    /// <summary>Runs the operation on the focused window.</summary>
    public virtual bool Execute(OperationContext context) => ExecuteOn(context, context.Windows.FocusedWindow());

    /// <summary>Runs the operation on <paramref name="window"/>; operations that do not target a window ignore it.</summary>
    public abstract bool ExecuteOn(OperationContext context, IWindow? window);

    /// <summary>True when an undo snapshot should be taken before running this operation.</summary>
    public bool TakesUndoSnapshot(Configuration.SlateSettings settings) =>
        settings.GetCommaList(Configuration.ConfigKeys.UndoOps).Contains(Kind.ConfigName());
}
