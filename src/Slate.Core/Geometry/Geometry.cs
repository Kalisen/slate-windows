namespace Slate.Core.Geometry;

/// <summary>A point in virtual-screen coordinates: origin at the top-left of the primary screen, y grows downwards.</summary>
public readonly record struct Point(double X, double Y);

public readonly record struct Size(double Width, double Height);

/// <summary>An axis-aligned rectangle in virtual-screen coordinates (top-left origin).</summary>
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public static readonly Rect Empty = new(0, 0, 0, 0);

    public Rect(Point topLeft, Size size) : this(topLeft.X, topLeft.Y, size.Width, size.Height) { }

    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public Point TopLeft => new(X, Y);
    public Size Size => new(Width, Height);
    public Point Center => new(X + Width / 2, Y + Height / 2);
    public double Area => Width * Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>The overlapping area of both rectangles, or <see cref="Empty"/> when they do not overlap.</summary>
    public Rect Intersect(Rect other)
    {
        double left = Math.Max(Left, other.Left);
        double top = Math.Max(Top, other.Top);
        double right = Math.Min(Right, other.Right);
        double bottom = Math.Min(Bottom, other.Bottom);
        return right > left && bottom > top ? new Rect(left, top, right - left, bottom - top) : Empty;
    }

    /// <summary>Inclusive of all edges, matching the original screen hit testing.</summary>
    public bool Contains(Point point) =>
        point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;

    public Rect Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };
}
