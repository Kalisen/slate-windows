namespace Slate.Core.Configuration;

/// <summary>What a percentage in <c>nudge</c>/<c>resize</c> is relative to.</summary>
public enum PercentOf
{
    ScreenSize,
    WindowSize,
}

public enum WindowHintsOrder
{
    None,
    Persist,
    LeftToRight,
    RightToLeft,
}

public enum SwitchOrientation
{
    Horizontal,
    Vertical,
}

/// <summary>How <c>activate-snapshot</c> pairs current windows with saved ones.</summary>
public enum SnapshotTitleMatch
{
    Levenshtein,
    Sequential,
}

/// <summary>An RGBA color written as <c>r;g;b;a</c> with 0-255 channels and a 0-1 alpha.</summary>
public readonly record struct RgbaColor(byte R, byte G, byte B, double Alpha)
{
    public static bool TryParse(string text, out RgbaColor color)
    {
        color = default;
        string[] parts = text.Split(';');
        if (parts.Length < 4) return false;
        var c = System.Globalization.CultureInfo.InvariantCulture;
        if (!double.TryParse(parts[0], c, out double r) || !double.TryParse(parts[1], c, out double g) ||
            !double.TryParse(parts[2], c, out double b) || !double.TryParse(parts[3], c, out double a))
            return false;
        color = new RgbaColor(ToByte(r), ToByte(g), ToByte(b), Math.Clamp(a, 0, 1));
        return true;
    }

    private static byte ToByte(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);
}
