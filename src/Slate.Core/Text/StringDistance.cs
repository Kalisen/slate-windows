namespace Slate.Core.Text;

/// <summary>Case-insensitive title distances used to match windows against snapshot entries.</summary>
public static class StringDistance
{
    public static int Levenshtein(string a, string b)
    {
        a = a.Trim().ToLowerInvariant();
        b = b.Trim().ToLowerInvariant();
        if (a.Length == 0 || b.Length == 0) return 0;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) previous[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    /// <summary>Length of the common prefix: higher means closer.</summary>
    public static int Sequential(string a, string b)
    {
        a = a.Trim().ToLowerInvariant();
        b = b.Trim().ToLowerInvariant();
        int n = Math.Min(a.Length, b.Length);
        int i = 0;
        while (i < n && a[i] == b[i]) i++;
        return i;
    }
}
