using Slate.Core.Configuration;
using Slate.Core.Operations;
using Slate.Core.Text;

namespace Slate.Core.Layouts;

/// <summary>Per-application options of a layout line: <c>'App':IGNORE_FAIL,REPEAT ...</c>.</summary>
public sealed record ApplicationOptions
{
    public const string IgnoreFailOption = "IGNORE_FAIL";
    public const string RepeatOption = "REPEAT";
    public const string RepeatLastOption = "REPEAT_LAST";
    public const string MainFirstOption = "MAIN_FIRST";
    public const string MainLastOption = "MAIN_LAST";
    public const string SortTitleOption = "SORT_TITLE";
    public const string TitleOrderPrefix = "TITLE_ORDER=";
    public const string TitleOrderRegexPrefix = "TITLE_ORDER_REGEX=";

    public bool IgnoreFail { get; init; }
    public bool Repeat { get; init; }
    public bool RepeatLast { get; init; }
    public bool MainFirst { get; init; }
    public bool MainLast { get; init; }
    public bool SortTitle { get; init; }
    public IReadOnlyList<string>? TitleOrder { get; init; }
    public IReadOnlyList<string>? TitleOrderRegex { get; init; }

    public static ApplicationOptions Parse(string options)
    {
        var result = new ApplicationOptions();
        foreach (string option in options.Split(','))
        {
            result = option switch
            {
                IgnoreFailOption => result with { IgnoreFail = true },
                RepeatOption => result with { Repeat = true },
                RepeatLastOption => result with { RepeatLast = true },
                MainFirstOption => result with { MainFirst = true },
                MainLastOption => result with { MainLast = true },
                SortTitleOption => result with { SortTitle = true },
                _ when option.StartsWith(TitleOrderRegexPrefix, StringComparison.Ordinal) =>
                    result with { TitleOrderRegex = option[TitleOrderRegexPrefix.Length..].Split(';') },
                _ when option.StartsWith(TitleOrderPrefix, StringComparison.Ordinal) =>
                    result with { TitleOrder = option[TitleOrderPrefix.Length..].Split(';') },
                _ => result,
            };
        }
        return result;
    }
}

/// <summary>Operations to run per application window, built from one or more <c>layout</c> directives with the same name.</summary>
public sealed class Layout(string name)
{
    /// <summary>Application name that runs its operation before the applications are arranged.</summary>
    public const string Before = "BEFORE";
    /// <summary>Application name that runs its operation after the applications are arranged.</summary>
    public const string After = "AFTER";

    private readonly Dictionary<string, IReadOnlyList<Operation>> appOperations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ApplicationOptions> appOptions = new(StringComparer.Ordinal);
    private readonly List<string> appOrder = [];

    public string Name { get; } = name;
    public List<Operation> BeforeOperations { get; } = [];
    public List<Operation> AfterOperations { get; } = [];

    /// <summary>Application names in the order of their (last) layout line.</summary>
    public IReadOnlyList<string> AppOrder => appOrder;

    public IReadOnlyList<Operation> OperationsFor(string app) => appOperations[app];

    public ApplicationOptions OptionsFor(string app) => appOptions[app];

    /// <summary>Adds a <c>layout name 'app name':OPTIONS op | op ...</c> line.</summary>
    public void Add(string line, ParseContext context)
    {
        var tokens = StringTokenizer.Tokenize(line, 4, StringTokenizer.Quotes);
        if (tokens.Count <= 3) throw new ConfigException("Unrecognized Layout", line);
        string[] appAndOptions = SplitAppAndOptions(tokens[2]);
        string app = appAndOptions[0];
        string operations = tokens[3];
        if (app == Before)
        {
            BeforeOperations.Add(OperationParser.Parse(operations, context));
            return;
        }
        if (app == After)
        {
            AfterOperations.Add(OperationParser.Parse(operations, context));
            return;
        }
        appOrder.Remove(app);
        appOrder.Add(app);
        appOptions[app] = appAndOptions.Length > 1 ? ApplicationOptions.Parse(appAndOptions[1]) : new ApplicationOptions();
        appOperations[app] = operations.Split(OperationSeparators.Pipe).Select(op => OperationParser.Parse(op, context)).ToList();
    }

    /// <summary>Splits <c>App Name:OPTIONS</c> at the first colon.</summary>
    private static string[] SplitAppAndOptions(string token)
    {
        int colon = token.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? [token] : [token[..colon], token[(colon + 1)..]];
    }
}
