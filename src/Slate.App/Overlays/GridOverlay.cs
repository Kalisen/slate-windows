using System.Drawing;
using Slate.Core.Configuration;
using Slate.Core.Interaction;
using CorePoint = Slate.Core.Geometry.Point;
using CoreSize = Slate.Core.Geometry.Size;

namespace Slate.App.Overlays;

/// <summary>A grid centered on one screen. Dragging over cells selects where the window goes.</summary>
internal sealed class GridOverlay : LayeredOverlay
{
    /// <summary>The grid takes a quarter of the screen's width and height.</summary>
    private const int ScreenFraction = 4;

    private readonly GridSpec spec;
    private readonly SlateSettings settings;
    private readonly Action<GridSelection> onSelected;
    private readonly GridGeometry geometry;
    private Point? dragStart;
    private GridSelection? highlighted;

    public GridOverlay(GridSpec spec, SlateSettings settings, Action<GridSelection> onSelected)
        : base(GridBounds(spec), clickThrough: false)
    {
        this.spec = spec;
        this.settings = settings;
        this.onSelected = onSelected;
        geometry = new GridGeometry(new CoreSize(Width, Height), spec.Columns, spec.Rows, spec.Padding * spec.Scale);
        Cursor = Cursors.Cross;
    }

    private static Rectangle GridBounds(GridSpec spec)
    {
        var f = spec.ScreenFrame;
        int width = (int)(f.Width / ScreenFraction), height = (int)(f.Height / ScreenFraction);
        return new Rectangle((int)(f.X + (f.Width - width) / 2), (int)(f.Y + (f.Height - height) / 2), width, height);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        dragStart = e.Location;
        Highlight(e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragStart is not null) Highlight(e.Location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (dragStart is not { } start || e.Button != MouseButtons.Left) return;
        dragStart = null;
        onSelected(Selection(start, e.Location));
    }

    private void Highlight(Point end)
    {
        var selection = Selection(dragStart!.Value, end);
        if (selection == highlighted) return;
        highlighted = selection;
        Render();
    }

    private GridSelection Selection(Point start, Point end) =>
        geometry.Select(spec.ScreenId, new CorePoint(start.X, start.Y), new CorePoint(end.X, end.Y));

    protected override void Draw(Graphics g)
    {
        float scale = (float)spec.Scale;
        using (var path = RoundedRectangle(new RectangleF(0, 0, Width, Height), (float)settings.GetDouble(ConfigKeys.GridRoundedCornerSize) * scale))
        using (var background = new SolidBrush(ToColor(settings.GetColor(ConfigKeys.GridBackgroundColor))))
            g.FillPath(background, path);

        using var cell = new SolidBrush(ToColor(settings.GetColor(ConfigKeys.GridCellBackgroundColor)));
        using var selected = new SolidBrush(ToColor(settings.GetColor(ConfigKeys.GridCellSelectedColor)));
        float corner = (float)settings.GetDouble(ConfigKeys.GridCellRoundedCornerSize) * scale;
        for (int row = 0; row < spec.Rows; row++)
        {
            for (int column = 0; column < spec.Columns; column++)
            {
                var b = geometry.CellBounds(column, row);
                using var path = RoundedRectangle(new RectangleF((float)b.X, (float)b.Y, (float)b.Width, (float)b.Height), corner);
                g.FillPath(IsHighlighted(column, row) ? selected : cell, path);
            }
        }
    }

    private bool IsHighlighted(int column, int row) =>
        highlighted is { } s && column >= s.Column && column < s.Column + s.ColumnSpan && row >= s.Row && row < s.Row + s.RowSpan;
}

internal sealed class GridView : IGridView
{
    private readonly List<GridOverlay> overlays;

    public GridView(IReadOnlyList<GridSpec> grids, SlateSettings settings, Action<GridSelection> onSelected)
    {
        overlays = grids.Select(g => new GridOverlay(g, settings, onSelected)).ToList();
        foreach (var overlay in overlays) overlay.ShowOverlay();
    }

    public void Close()
    {
        foreach (var overlay in overlays) overlay.Close();
        overlays.Clear();
    }
}
