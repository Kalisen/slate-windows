using Slate.Core.Configuration;
using Slate.Core.Expressions;
using Slate.Core.Screens;
using Slate.Core.Text;
using static Slate.Core.Expressions.ExpressionVariables;

namespace Slate.Core.Operations;

/// <summary>Parsers for <c>move</c>, <c>push</c>, <c>nudge</c>, <c>throw</c> and <c>corner</c>.</summary>
public static class MoveOperations
{
    public const string StyleNone = "none";
    public const string StyleCenter = "center";
    public const string StyleBar = "bar";
    public const string StyleBarResizePrefix = "bar-resize:";
    public const string StyleResize = "resize";
    public const string StyleNoResize = "noresize";
    public const string StyleResizePrefix = "resize:";

    private static readonly ExpressionPoint CurrentSize = new(WindowSizeX, WindowSizeY);

    /// <summary><c>move topLeftX;topLeftY width;height [screen]</c></summary>
    public static MoveOperation ParseMove(string text)
    {
        var tokens = StringTokenizer.Tokenize(text);
        if (tokens.Count < 3)
            throw InvalidParameters(text, "Move operations require the following format: 'move topLeftX;topLeftY width;height [optional:screen]'");
        var topLeft = ExpressionPoint.Parse(tokens[1]) ?? throw InvalidParameters(text, "topLeft must be 'x;y'");
        var size = ExpressionPoint.Parse(tokens[2]) ?? throw InvalidParameters(text, "size must be 'width;height'");
        return Build(OperationKind.Move, text, topLeft, size, ScreenArgument(tokens, 3));
    }

    /// <summary><c>push direction [style] [screen]</c></summary>
    public static MoveOperation ParsePush(string text)
    {
        var tokens = StringTokenizer.Tokenize(text);
        if (tokens.Count < 2)
            throw InvalidParameters(text, "Push operations require the following format: 'push direction [optional:style]'");
        string style = tokens.Count >= 3 ? tokens[2] : StyleNone;
        Direction direction = Directions.Parse(tokens[1]) is { } d and not Direction.Behind
            ? d
            : throw new ConfigException("Unrecognized Direction", $"Unrecognized direction '{tokens[1]}' in '{text}'");
        var (topLeft, size) = PushExpressions(direction, style)
            ?? throw new ConfigException("Unrecognized Style", $"Unrecognized style '{style}' in '{text}'");
        return Build(OperationKind.Push, text, topLeft, size, ScreenArgument(tokens, 3));
    }

    private static (ExpressionPoint TopLeft, ExpressionPoint Size)? PushExpressions(Direction direction, string style)
    {
        string? bar = style.StartsWith(StyleBarResizePrefix, StringComparison.Ordinal) ? style[StyleBarResizePrefix.Length..] : null;
        const string centerX = $"{ScreenOriginX}+({ScreenSizeX}-{WindowSizeX})/2";
        const string centerY = $"{ScreenOriginY}+({ScreenSizeY}-{WindowSizeY})/2";
        const string rightX = $"{ScreenOriginX}+{ScreenSizeX}-{WindowSizeX}";
        const string bottomY = $"{ScreenOriginY}+{ScreenSizeY}-{WindowSizeY}";
        return (direction, style) switch
        {
            (Direction.Up, StyleNone) => (new(WindowTopLeftX, ScreenOriginY), CurrentSize),
            (Direction.Up, StyleCenter) => (new(centerX, ScreenOriginY), CurrentSize),
            (Direction.Up, StyleBar) => (new(ScreenOriginX, ScreenOriginY), new(ScreenSizeX, WindowSizeY)),
            (Direction.Up, _) when bar is not null => (new(ScreenOriginX, ScreenOriginY), new(ScreenSizeX, bar)),
            (Direction.Down, StyleNone) => (new(WindowTopLeftX, bottomY), CurrentSize),
            (Direction.Down, StyleCenter) => (new(centerX, bottomY), CurrentSize),
            (Direction.Down, StyleBar) => (new(ScreenOriginX, bottomY), new(ScreenSizeX, WindowSizeY)),
            (Direction.Down, _) when bar is not null => (new(ScreenOriginX, $"{ScreenOriginY}+{ScreenSizeY}-({bar})"), new(ScreenSizeX, bar)),
            (Direction.Left, StyleNone) => (new(ScreenOriginX, WindowTopLeftY), CurrentSize),
            (Direction.Left, StyleCenter) => (new(ScreenOriginX, centerY), CurrentSize),
            (Direction.Left, StyleBar) => (new(ScreenOriginX, ScreenOriginY), new(WindowSizeX, ScreenSizeY)),
            (Direction.Left, _) when bar is not null => (new(ScreenOriginX, ScreenOriginY), new(bar, ScreenSizeY)),
            (Direction.Right, StyleNone) => (new(rightX, WindowTopLeftY), CurrentSize),
            (Direction.Right, StyleCenter) => (new(rightX, centerY), CurrentSize),
            (Direction.Right, StyleBar) => (new(rightX, ScreenOriginY), new(WindowSizeX, ScreenSizeY)),
            (Direction.Right, _) when bar is not null => (new($"{ScreenOriginX}+{ScreenSizeX}-({bar})", ScreenOriginY), new(bar, ScreenSizeY)),
            _ => null,
        };
    }

    /// <summary><c>nudge x y</c> where x and y are <c>+10%</c> or <c>-100</c>.</summary>
    public static MoveOperation ParseNudge(string text, SlateSettings settings)
    {
        var tokens = StringTokenizer.Tokenize(text);
        if (tokens.Count < 3)
            throw InvalidParameters(text, "Nudge operations require the following format: 'nudge x y'");
        string percentOf = PercentOfVariablePrefix(settings.GetEnum<PercentOf>(ConfigKeys.NudgePercentOf));
        var topLeft = new ExpressionPoint(
            WindowTopLeftX + Offset(tokens[1], percentOf + "X"),
            WindowTopLeftY + Offset(tokens[2], percentOf + "Y"));
        return Build(OperationKind.Nudge, text, topLeft, CurrentSize, ScreenReferences.Current);
    }

    /// <summary><c>throw screen [resize|noresize|resize:width;height]</c></summary>
    public static MoveOperation ParseThrow(string text)
    {
        var tokens = StringTokenizer.Tokenize(text);
        if (tokens.Count < 2)
            throw InvalidParameters(text, "Throw operations require the following format: 'throw screen [optional:style]'");
        var topLeft = new ExpressionPoint(ScreenOriginX, ScreenOriginY);
        ExpressionPoint size = CurrentSize;
        if (tokens.Count >= 3)
        {
            string style = tokens[2];
            if (style == StyleResize) size = new ExpressionPoint(ScreenSizeX, ScreenSizeY);
            else if (style.StartsWith(StyleResizePrefix, StringComparison.Ordinal))
                size = ExpressionPoint.Parse(style[StyleResizePrefix.Length..]) ?? throw UnrecognizedStyle(style, text);
            else if (style != StyleNoResize) throw UnrecognizedStyle(style, text);
        }
        return Build(OperationKind.Throw, text, topLeft, size, tokens[1]);
    }

    /// <summary><c>corner top-left|top-right|bottom-left|bottom-right [resize:width;height] [screen]</c></summary>
    public static MoveOperation ParseCorner(string text)
    {
        var tokens = StringTokenizer.Tokenize(text);
        if (tokens.Count < 2)
            throw InvalidParameters(text, "Corner operations require the following format: 'corner direction [optional:style]'");
        ExpressionPoint size = CurrentSize;
        if (tokens.Count >= 3 && tokens[2].StartsWith(StyleResizePrefix, StringComparison.Ordinal))
            size = ExpressionPoint.Parse(tokens[2][StyleResizePrefix.Length..]) ?? throw UnrecognizedStyle(tokens[2], text);
        string right = $"{ScreenOriginX}+{ScreenSizeX}-({size.X})";
        string bottom = $"{ScreenOriginY}+{ScreenSizeY}-({size.Y})";
        ExpressionPoint topLeft = Corners.Parse(tokens[1]) switch
        {
            Corner.TopLeft => new(ScreenOriginX, ScreenOriginY),
            Corner.TopRight => new(right, ScreenOriginY),
            Corner.BottomLeft => new(ScreenOriginX, bottom),
            Corner.BottomRight => new(right, bottom),
            _ => throw new ConfigException("Unrecognized Corner", $"Unrecognized corner '{tokens[1]}' in '{text}'"),
        };
        return Build(OperationKind.Corner, text, topLeft, size, ScreenArgument(tokens, 3));
    }

    /// <summary>The expression prefix for a percentage base: <c>screenSize</c> or <c>windowSize</c>.</summary>
    public static string PercentOfVariablePrefix(PercentOf percentOf) =>
        percentOf == PercentOf.WindowSize ? WindowSize : ScreenSize;

    /// <summary>Turns <c>+10%</c> into <c>+10*screenSizeX/100</c> and <c>100</c> into <c>+100</c>.</summary>
    private static string Offset(string value, string percentVariable)
    {
        string signed = value.StartsWith('+') || value.StartsWith('-') ? value : "+" + value;
        return signed.EndsWith('%') ? signed[..^1] + $"*{percentVariable}/100" : signed;
    }

    private static string ScreenArgument(List<string> tokens, int index) =>
        tokens.Count > index ? tokens[index] : ScreenReferences.Current;

    private static MoveOperation Build(OperationKind kind, string text, ExpressionPoint topLeft, ExpressionPoint size, string screen)
    {
        ParseContext.ValidateExpressions(text, topLeft, size);
        return new MoveOperation(kind, topLeft, size, screen);
    }

    private static ConfigException InvalidParameters(string text, string format) =>
        new("Invalid Parameters", $"Invalid Parameters in '{text}'. {format}");

    private static ConfigException UnrecognizedStyle(string style, string text) =>
        new("Unrecognized Style", $"Unrecognized style '{style}' in '{text}'");
}
