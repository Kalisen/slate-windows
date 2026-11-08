using Slate.Core.Geometry;

namespace Slate.Core.Interaction;

/// <summary>Cell layout of a grid overlay and conversion of a mouse drag into a <see cref="GridSelection"/>.</summary>
public sealed class GridGeometry(Size viewSize, int columns, int rows, double padding)
{
    public double CellWidth => (viewSize.Width - padding * 2 - (columns - 1) * padding) / columns;
    public double CellHeight => (viewSize.Height - padding * 2 - (rows - 1) * padding) / rows;

    /// <summary>The bounds of a cell inside the view; (0,0) is the top-left cell.</summary>
    public Rect CellBounds(int column, int row) =>
        new(padding + column * (CellWidth + padding), padding + row * (CellHeight + padding), CellWidth, CellHeight);

    /// <summary>The cells covered by a drag from <paramref name="begin"/> to <paramref name="end"/> (view coordinates).</summary>
    public GridSelection Select(int screenId, Point begin, Point end)
    {
        double bx = Math.Clamp(begin.X, 0, viewSize.Width), ex = Math.Clamp(end.X, 0, viewSize.Width);
        double by = Math.Clamp(begin.Y, 0, viewSize.Height), ey = Math.Clamp(end.Y, 0, viewSize.Height);
        int c0 = Cell(Math.Min(bx, ex), CellWidth, columns), c1 = Cell(Math.Max(bx, ex), CellWidth, columns);
        int r0 = Cell(Math.Min(by, ey), CellHeight, rows), r1 = Cell(Math.Max(by, ey), CellHeight, rows);
        return new GridSelection(screenId, c0, r0, c1 - c0 + 1, r1 - r0 + 1);
    }

    private int Cell(double position, double cellLength, int count) =>
        Math.Min((int)(count * (position / (cellLength * count + padding * (count + 1)))), count - 1);
}
