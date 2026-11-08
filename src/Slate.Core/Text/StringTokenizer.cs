using System.Text;

namespace Slate.Core.Text;

/// <summary>Whitespace tokenizer used by every directive and operation parser.</summary>
public static class StringTokenizer
{
    /// <summary>Single and double quotes, the quote characters accepted by the config format.</summary>
    public const string Quotes = "'\"";

    public static bool IsSpace(char c) => c is ' ' or '\t';

    /// <summary>
    /// Splits <paramref name="s"/> on whitespace. Once <paramref name="maxTokens"/> - 1 tokens are produced the rest of the
    /// string (without its leading whitespace) becomes the last token. When <paramref name="quoteChars"/> is given, quoted
    /// sections keep their whitespace and the surrounding quotes are dropped; quotes are not interpreted in the remainder.
    /// </summary>
    public static List<string> Tokenize(string s, int maxTokens = int.MaxValue, string? quoteChars = null)
    {
        var tokens = new List<string>();
        if (maxTokens <= 1)
        {
            if (s.Length > 0) tokens.Add(s);
            return tokens;
        }

        var token = new StringBuilder();
        char? openQuote = null;
        foreach (char c in s)
        {
            bool inRemainder = tokens.Count >= maxTokens - 1;
            if (inRemainder)
            {
                if (token.Length == 0 && IsSpace(c)) continue;
                token.Append(c);
            }
            else if (IsSpace(c) && openQuote is null)
            {
                if (token.Length > 0)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                }
            }
            else if (quoteChars is not null && quoteChars.Contains(c) && (openQuote is null || openQuote == c))
            {
                openQuote = openQuote is null ? c : null;
            }
            else
            {
                token.Append(c);
            }
        }
        if (token.Length > 0) tokens.Add(token.ToString());
        return tokens;
    }

    public static string FirstToken(string s)
    {
        var tokens = Tokenize(s, 2);
        return tokens.Count > 0 ? tokens[0] : string.Empty;
    }

    /// <summary>Removes a pair of surrounding quote characters, if present.</summary>
    public static string RemoveQuotes(string s, string quoteChars = Quotes)
    {
        if (s.Length >= 2 && quoteChars.Contains(s[0]) && quoteChars.Contains(s[^1])) return s[1..^1];
        return s;
    }

    public static bool IsQuoted(string s, string quoteChars = Quotes) =>
        s.Length >= 2 && quoteChars.Contains(s[0]) && quoteChars.Contains(s[^1]);
}
