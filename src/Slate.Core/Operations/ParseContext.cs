using Slate.Core.Configuration;
using Slate.Core.Expressions;
using Slate.Core.Keyboard;
using Slate.Core.Layouts;

namespace Slate.Core.Operations;

/// <summary>State available while parsing operations: directives are read top-down, so only earlier ones are visible.</summary>
public sealed class ParseContext(SlateSettings settings, IReadOnlyDictionary<string, Layout> layouts, IKeyboardLayout keyboardLayout, string homeDirectory)
{
    /// <summary>A plausible window used to check move expressions while parsing.</summary>
    private const double SampleExtent = 1000;

    private static readonly IReadOnlyDictionary<string, double> SampleValues = new Dictionary<string, double>
    {
        [ExpressionVariables.ScreenOriginX] = 0,
        [ExpressionVariables.ScreenOriginY] = 0,
        [ExpressionVariables.ScreenSizeX] = SampleExtent,
        [ExpressionVariables.ScreenSizeY] = SampleExtent,
        [ExpressionVariables.WindowTopLeftX] = 0,
        [ExpressionVariables.WindowTopLeftY] = 0,
        [ExpressionVariables.WindowSizeX] = SampleExtent,
        [ExpressionVariables.WindowSizeY] = SampleExtent,
        [ExpressionVariables.NewWindowSizeX] = SampleExtent,
        [ExpressionVariables.NewWindowSizeY] = SampleExtent,
    };

    public SlateSettings Settings { get; } = settings;
    public IReadOnlyDictionary<string, Layout> Layouts { get; } = layouts;
    public IKeyboardLayout KeyboardLayout { get; } = keyboardLayout;

    /// <summary>The directory <c>~</c> expands to.</summary>
    public string HomeDirectory { get; } = homeDirectory;

    /// <summary>Set once an <c>undo</c> operation is parsed: undoable operations then record snapshots.</summary>
    public bool UsesUndo { get; set; }

    public string ExpandHome(string path) => ExpandHome(path, HomeDirectory);

    /// <summary>Replaces a leading <c>~</c> with <paramref name="homeDirectory"/>.</summary>
    public static string ExpandHome(string path, string homeDirectory)
    {
        if (path == "~") return homeDirectory;
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            return Path.Combine(homeDirectory, path[2..]);
        return path;
    }

    /// <summary>Throws a <see cref="ConfigException"/> when an expression cannot be evaluated for a window.</summary>
    public static void ValidateExpressions(string operation, params ExpressionPoint[] points)
    {
        try
        {
            foreach (var point in points)
            {
                ExpressionEvaluator.Evaluate(point.X, SampleValues);
                ExpressionEvaluator.Evaluate(point.Y, SampleValues);
            }
        }
        catch (ExpressionException ex)
        {
            throw new ConfigException("Unable To Parse Binding", $"Unable to parse '{ex.Message}' in '{operation}'");
        }
    }
}
