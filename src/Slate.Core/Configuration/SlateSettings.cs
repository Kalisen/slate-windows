using System.Globalization;

namespace Slate.Core.Configuration;

/// <summary>Values set with the <c>config</c> directive, falling back to <see cref="ConfigDefaults"/>.</summary>
public sealed class SlateSettings
{
    private readonly Dictionary<string, string> values = new(ConfigDefaults.Values);
    private readonly Dictionary<string, Dictionary<string, string>> appValues = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string key) => ConfigDefaults.Values.ContainsKey(key);

    public void Set(string key, string value)
    {
        if (!IsKnown(key)) throw new ConfigException("Unrecognized Config", $"Unrecognized config '{key}'");
        values[key] = value;
    }

    /// <summary>Sets a config that only applies to windows of <paramref name="app"/>.</summary>
    public void SetForApp(string key, string app, string value)
    {
        if (!IsKnown(key)) throw new ConfigException("Unrecognized Config", $"Unrecognized config '{key}'");
        if (!appValues.TryGetValue(app, out var forApp)) appValues[app] = forApp = [];
        forApp[key] = value;
    }

    public string Get(string key) => values[key];

    /// <summary>The app-specific value of <paramref name="key"/>, or its global value.</summary>
    public string GetForApp(string key, string app) =>
        appValues.TryGetValue(app, out var forApp) && forApp.TryGetValue(key, out string? value) ? value : Get(key);

    public bool GetBool(string key) => ParseBool(Get(key));

    public int GetInt(string key) =>
        int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : (int)GetDouble(key);

    public double GetDouble(string key) =>
        double.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;

    /// <summary>A semicolon separated list, e.g. the candidate hint positions.</summary>
    public IReadOnlyList<string> GetList(string key) => Get(key).Split(';');

    /// <summary>A comma separated list, e.g. the operations that repeat while held.</summary>
    public IReadOnlyList<string> GetCommaList(string key) =>
        Get(key).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public TEnum GetEnum<TEnum>(string key) where TEnum : struct, Enum => ParseEnum<TEnum>(Get(key), ConfigDefaults.Values[key]);

    public RgbaColor GetColor(string key) =>
        RgbaColor.TryParse(Get(key), out var color) ? color : ParseColorOrThrow(ConfigDefaults.Values[key]);

    public static TEnum ParseEnum<TEnum>(string value, string fallback) where TEnum : struct, Enum
    {
        string normalized = value.Replace("-", "", StringComparison.Ordinal);
        if (Enum.TryParse(normalized, ignoreCase: true, out TEnum parsed) && Enum.IsDefined(parsed)) return parsed;
        return Enum.Parse<TEnum>(fallback.Replace("-", "", StringComparison.Ordinal), ignoreCase: true);
    }

    /// <summary>Accepts the spellings NSString.boolValue accepted: true/yes/1 (case-insensitive).</summary>
    public static bool ParseBool(string value) =>
        value.Trim().ToLowerInvariant() is "true" or "yes" or "y" or "t" ||
        (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && i != 0);

    private static RgbaColor ParseColorOrThrow(string text) =>
        RgbaColor.TryParse(text, out var color) ? color : throw new InvalidOperationException($"Invalid default color {text}");
}
