namespace Slate.Core.Expressions;

/// <summary>Names of the variables available in move/resize expressions.</summary>
public static class ExpressionVariables
{
    public const string ScreenOriginX = "screenOriginX";
    public const string ScreenOriginY = "screenOriginY";
    public const string ScreenSizeX = "screenSizeX";
    public const string ScreenSizeY = "screenSizeY";
    public const string WindowTopLeftX = "windowTopLeftX";
    public const string WindowTopLeftY = "windowTopLeftY";
    public const string WindowSizeX = "windowSizeX";
    public const string WindowSizeY = "windowSizeY";
    public const string NewWindowSizeX = "newWindowSizeX";
    public const string NewWindowSizeY = "newWindowSizeY";
    public const string WindowHintsWidth = "windowHintsWidth";
    public const string WindowHintsHeight = "windowHintsHeight";

    /// <summary>Prefix shared by <see cref="ScreenSizeX"/>/<see cref="ScreenSizeY"/>, used by "percent of" configs.</summary>
    public const string ScreenSize = "screenSize";
    /// <summary>Prefix shared by <see cref="WindowSizeX"/>/<see cref="WindowSizeY"/>, used by "percent of" configs.</summary>
    public const string WindowSize = "windowSize";
}

/// <summary>A pair of expressions evaluated together, e.g. a top-left corner or a size.</summary>
public sealed record ExpressionPoint(string X, string Y)
{
    /// <summary>Parses <c>x;y</c> (or <c>x,y</c>) into a pair, or returns null when the text is not a pair.</summary>
    public static ExpressionPoint? Parse(string text)
    {
        foreach (char separator in new[] { ';', ',' })
        {
            string[] parts = text.Split(separator);
            if (parts.Length == 2) return new ExpressionPoint(parts[0], parts[1]);
        }
        return null;
    }

    public Geometry.Point EvaluatePoint(IReadOnlyDictionary<string, double> values) =>
        new(ExpressionEvaluator.Evaluate(X, values), ExpressionEvaluator.Evaluate(Y, values));

    public Geometry.Size EvaluateSize(IReadOnlyDictionary<string, double> values) =>
        new(ExpressionEvaluator.Evaluate(X, values), ExpressionEvaluator.Evaluate(Y, values));
}
