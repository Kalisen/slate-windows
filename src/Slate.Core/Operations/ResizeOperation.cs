using System.Globalization;
using Slate.Core.Configuration;
using Slate.Core.Geometry;
using Slate.Core.Screens;
using Slate.Core.Text;
using Slate.Core.Windowing;

namespace Slate.Core.Operations;

/// <summary><c>resize x y [anchor]</c>: grows or shrinks the window by an amount or a percentage, keeping a corner in place.</summary>
public sealed class ResizeOperation(string widthDelta, string heightDelta, Corner anchor) : Operation
{
    public const char Percent = '%';

    public override OperationKind Kind => OperationKind.Resize;
    public string WidthDelta { get; } = widthDelta;
    public string HeightDelta { get; } = heightDelta;
    public Corner Anchor { get; } = anchor;

    public static ResizeOperation Parse(string text)
    {
        var tokens = StringTokenizer.Tokenize(text);
        if (tokens.Count < 3)
            throw new ConfigException("Invalid Parameters",
                $"Invalid Parameters in '{text}'. Resize operations require the following format: 'resize resizeX resizeY [optional:anchor]'");
        Corner anchor = tokens.Count >= 4
            ? Corners.Parse(tokens[3]) ?? throw new ConfigException("Unrecognized Anchor", $"Unrecognized anchor '{tokens[3]}' in '{text}'")
            : Corner.TopLeft;
        foreach (string delta in new[] { tokens[1], tokens[2] })
        {
            if (!TryParseDelta(delta, 0, out _))
                throw new ConfigException("Invalid Parameters", $"Invalid resize amount '{delta}' in '{text}'");
        }
        return new ResizeOperation(tokens[1], tokens[2], anchor);
    }

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        if (window is null) return false;
        Rect current = window.Frame;
        Size newSize = ComputeSize(context, current);
        if (newSize == current.Size) return false;
        bool success = window.Resize(newSize);
        success = window.Move(AnchoredTopLeft(current, window.Frame.Size)) && success;
        return success;
    }

    public Size ComputeSize(OperationContext context, Rect current)
    {
        Size reference = current.Size;
        if (context.Settings.GetEnum<PercentOf>(ConfigKeys.ResizePercentOf) == PercentOf.ScreenSize)
        {
            ScreenLayout screens = context.Screens();
            int screenId = screens.Resolve(ScreenReferences.Current, current);
            if (!screens.Exists(screenId)) return current.Size;
            reference = screens.VisibleFrame(screenId).Size;
        }
        TryParseDelta(WidthDelta, reference.Width, out double dx);
        TryParseDelta(HeightDelta, reference.Height, out double dy);
        return new Size(current.Width + dx, current.Height + dy);
    }

    public Point AnchoredTopLeft(Rect current, Size newSize) => Anchor switch
    {
        Corner.TopRight => new(current.Right - newSize.Width, current.Y),
        Corner.BottomLeft => new(current.X, current.Bottom - newSize.Height),
        Corner.BottomRight => new(current.Right - newSize.Width, current.Bottom - newSize.Height),
        _ => current.TopLeft,
    };

    /// <summary>Parses <c>+10%</c> (of <paramref name="reference"/>, truncated) or <c>-100</c> into a signed amount.</summary>
    public static bool TryParseDelta(string delta, double reference, out double amount)
    {
        amount = 0;
        int sign = delta.StartsWith('-') ? -1 : 1;
        string magnitude = delta.TrimStart('+', '-');
        bool percent = magnitude.EndsWith(Percent);
        if (percent) magnitude = magnitude[..^1];
        if (!double.TryParse(magnitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) return false;
        amount = sign * (percent ? Math.Truncate(reference * value / 100) : value);
        return true;
    }
}
