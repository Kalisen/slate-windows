using System.Globalization;
using Slate.Core.Configuration;
using Slate.Core.Text;

namespace Slate.Core.Screens;

/// <summary>How a <c>default</c> directive describes the screen setup it applies to.</summary>
public enum ScreenSetupKind
{
    Count,
    Resolutions,
}

/// <summary>
/// The <c>default name count:N</c> / <c>default name resolutions:WxH;WxH</c> directive: activates a layout or snapshot
/// when the screens match.
/// </summary>
public sealed record DefaultLayout(string Name, ScreenSetupKind Kind, int Count, IReadOnlyList<string> Resolutions)
{
    public const string CountPrefix = "count";
    public const string ResolutionsPrefix = "resolutions";

    public static DefaultLayout Parse(string line)
    {
        var tokens = StringTokenizer.Tokenize(line);
        if (tokens.Count < 3) throw Invalid(line);
        string[] setup = tokens[2].Split(':');
        if (setup.Length < 2) throw Invalid(line);
        if (setup[0] == CountPrefix)
        {
            if (!int.TryParse(setup[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)) throw Invalid(line);
            return new DefaultLayout(tokens[1], ScreenSetupKind.Count, count, []);
        }
        if (setup[0] == ResolutionsPrefix)
        {
            var resolutions = setup[1].Split([';', ','], StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToList();
            return new DefaultLayout(tokens[1], ScreenSetupKind.Resolutions, 0, resolutions);
        }
        throw Invalid(line);
    }

    public bool Matches(ScreenLayout screens)
    {
        if (Kind == ScreenSetupKind.Count) return Count == screens.Count;
        var current = screens.ResolutionStrings().Order(StringComparer.Ordinal).ToList();
        return current.SequenceEqual(Resolutions);
    }

    /// <summary>The first default whose screen setup matches the current screens.</summary>
    public static DefaultLayout? FindMatch(IEnumerable<DefaultLayout> defaults, ScreenLayout screens) =>
        defaults.FirstOrDefault(d => d.Matches(screens));

    private static ConfigException Invalid(string line) => new("Error loading default layout", line);
}
