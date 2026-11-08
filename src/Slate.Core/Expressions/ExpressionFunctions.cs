namespace Slate.Core.Expressions;

/// <summary>Functions available in expressions, mirroring the NSExpression functions the macOS version exposed.</summary>
public enum ExpressionFunction
{
    Sum,
    Count,
    Min,
    Max,
    Average,
    Median,
    Stddev,
    Sqrt,
    Log,
    Ln,
    Exp,
    Floor,
    Ceiling,
    Abs,
    Trunc,
    Random,
    Randomn,
}

internal static class ExpressionFunctions
{
    public static double Call(string name, IReadOnlyList<double> args, Random random, string expression)
    {
        if (!Enum.TryParse(name, ignoreCase: true, out ExpressionFunction function) || !Enum.IsDefined(function))
            throw new ExpressionException($"Unknown function '{name}' in '{expression}'");

        return function switch
        {
            ExpressionFunction.Sum => args.Sum(),
            ExpressionFunction.Count => args.Count,
            ExpressionFunction.Min => NonEmpty(args, name, expression).Min(),
            ExpressionFunction.Max => NonEmpty(args, name, expression).Max(),
            ExpressionFunction.Average => NonEmpty(args, name, expression).Average(),
            ExpressionFunction.Median => Median(NonEmpty(args, name, expression)),
            ExpressionFunction.Stddev => StandardDeviation(NonEmpty(args, name, expression)),
            ExpressionFunction.Sqrt => Math.Sqrt(Single(args, name, expression)),
            ExpressionFunction.Log => Math.Log10(Single(args, name, expression)),
            ExpressionFunction.Ln => Math.Log(Single(args, name, expression)),
            ExpressionFunction.Exp => Math.Exp(Single(args, name, expression)),
            ExpressionFunction.Floor => Math.Floor(Single(args, name, expression)),
            ExpressionFunction.Ceiling => Math.Ceiling(Single(args, name, expression)),
            ExpressionFunction.Abs => Math.Abs(Single(args, name, expression)),
            ExpressionFunction.Trunc => Math.Truncate(Single(args, name, expression)),
            ExpressionFunction.Random => random.NextDouble(),
            ExpressionFunction.Randomn => random.Next((int)Single(args, name, expression)),
            _ => throw new ExpressionException($"Unknown function '{name}' in '{expression}'"),
        };
    }

    private static IReadOnlyList<double> NonEmpty(IReadOnlyList<double> args, string name, string expression) =>
        args.Count > 0 ? args : throw new ExpressionException($"{name}() needs at least one value in '{expression}'");

    private static double Single(IReadOnlyList<double> args, string name, string expression) =>
        args.Count == 1 ? args[0] : throw new ExpressionException($"{name}() takes one value in '{expression}'");

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.Order().ToArray();
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    /// <summary>Population standard deviation, as NSExpression computes it.</summary>
    private static double StandardDeviation(IReadOnlyList<double> values)
    {
        double mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }
}
