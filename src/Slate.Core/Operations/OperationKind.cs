namespace Slate.Core.Operations;

/// <summary>Every operation a binding, layout, chain or sequence can run.</summary>
public enum OperationKind
{
    Move,
    Resize,
    Push,
    Nudge,
    Throw,
    Corner,
    Chain,
    Layout,
    Focus,
    Snapshot,
    ActivateSnapshot,
    DeleteSnapshot,
    Hint,
    Switch,
    Grid,
    Sequence,
    Hide,
    Show,
    Toggle,
    Relaunch,
    Shell,
    Undo,
}

public static class OperationKinds
{
    private static readonly Dictionary<string, OperationKind> ByName = new()
    {
        ["move"] = OperationKind.Move,
        ["resize"] = OperationKind.Resize,
        ["push"] = OperationKind.Push,
        ["nudge"] = OperationKind.Nudge,
        ["throw"] = OperationKind.Throw,
        ["corner"] = OperationKind.Corner,
        ["chain"] = OperationKind.Chain,
        ["layout"] = OperationKind.Layout,
        ["focus"] = OperationKind.Focus,
        ["snapshot"] = OperationKind.Snapshot,
        ["activate-snapshot"] = OperationKind.ActivateSnapshot,
        ["delete-snapshot"] = OperationKind.DeleteSnapshot,
        ["hint"] = OperationKind.Hint,
        ["switch"] = OperationKind.Switch,
        ["grid"] = OperationKind.Grid,
        ["sequence"] = OperationKind.Sequence,
        ["hide"] = OperationKind.Hide,
        ["show"] = OperationKind.Show,
        ["toggle"] = OperationKind.Toggle,
        ["relaunch"] = OperationKind.Relaunch,
        ["shell"] = OperationKind.Shell,
        ["undo"] = OperationKind.Undo,
    };

    public static bool TryParse(string name, out OperationKind kind) => ByName.TryGetValue(name, out kind);

    /// <summary>The name used in configs, e.g. <c>activate-snapshot</c>.</summary>
    public static string ConfigName(this OperationKind kind) => ByName.First(p => p.Value == kind).Key;
}

/// <summary>A direction used by <c>push</c> and <c>focus</c>.</summary>
public enum Direction
{
    Up,
    Down,
    Left,
    Right,
    Behind,
}

public static class Directions
{
    public const string Up = "up";
    public const string Top = "top";
    public const string Above = "above";
    public const string Down = "down";
    public const string Bottom = "bottom";
    public const string Below = "below";
    public const string Left = "left";
    public const string Right = "right";
    public const string Behind = "behind";

    /// <summary>Parses a direction word, or returns null.</summary>
    public static Direction? Parse(string text) => text switch
    {
        Up or Top or Above => Direction.Up,
        Down or Bottom or Below => Direction.Down,
        Left => Direction.Left,
        Right => Direction.Right,
        Behind => Direction.Behind,
        _ => null,
    };
}

/// <summary>The corner of a window that stays in place during a <c>resize</c>, or the target of <c>corner</c>.</summary>
public enum Corner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public static class Corners
{
    public const string TopLeft = "top-left";
    public const string TopRight = "top-right";
    public const string BottomLeft = "bottom-left";
    public const string BottomRight = "bottom-right";

    public static Corner? Parse(string text) => text switch
    {
        TopLeft => Corner.TopLeft,
        TopRight => Corner.TopRight,
        BottomLeft => Corner.BottomLeft,
        BottomRight => Corner.BottomRight,
        _ => null,
    };
}
