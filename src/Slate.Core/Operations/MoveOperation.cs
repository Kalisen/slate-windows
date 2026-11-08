using Slate.Core.Expressions;
using Slate.Core.Geometry;
using Slate.Core.Screens;
using Slate.Core.Windowing;

namespace Slate.Core.Operations;

/// <summary>
/// Moves and resizes a window to positions given by expressions. <c>push</c>, <c>nudge</c>, <c>throw</c> and
/// <c>corner</c> are all moves with preset expressions.
/// </summary>
public sealed class MoveOperation : Operation
{
    private readonly OperationKind kind;

    /// <param name="screenReference">A screen reference resolved at run time, or null to use <paramref name="screenId"/>.</param>
    public MoveOperation(OperationKind kind, ExpressionPoint topLeft, ExpressionPoint size, string? screenReference, int screenId = ScreenIds.Ignore)
    {
        this.kind = kind;
        TopLeft = topLeft;
        Size = size;
        ScreenReference = screenReference;
        ScreenId = screenId;
    }

    public override OperationKind Kind => kind;
    public ExpressionPoint TopLeft { get; }
    public ExpressionPoint Size { get; }
    public string? ScreenReference { get; }
    public int ScreenId { get; }

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        if (window is null) return false;
        ScreenLayout screens = context.Screens();
        Rect current = window.Frame;
        Size newSize = ComputeSize(screens, current);
        bool success = window.Resize(newSize);
        Size actualSize = window.Frame.Size;
        Point topLeft = ComputeTopLeft(screens, current, actualSize);
        success = window.Move(topLeft) && success;
        // Moving to a screen with another scale factor can make the window rescale itself: apply the size again.
        success = window.Resize(newSize) && success;
        return success;
    }

    public Size ComputeSize(ScreenLayout screens, Rect current)
    {
        int screenId = TargetScreen(screens, current);
        return screens.Exists(screenId) ? Size.EvaluateSize(screens.ExpressionValues(screenId, current, current.Size)) : current.Size;
    }

    public Point ComputeTopLeft(ScreenLayout screens, Rect current, Size newSize)
    {
        int screenId = TargetScreen(screens, current);
        return screens.Exists(screenId) ? TopLeft.EvaluatePoint(screens.ExpressionValues(screenId, current, newSize)) : current.TopLeft;
    }

    private int TargetScreen(ScreenLayout screens, Rect current) =>
        ScreenReference is null ? ScreenId : screens.Resolve(ScreenReference, current);
}
