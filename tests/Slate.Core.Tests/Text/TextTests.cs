using Slate.Core.Text;

namespace Slate.Core.Tests.Text;

public class StringTokenizerTests
{
    [Theory]
    [InlineData("hi tokenize\t\t  me", new[] { "hi", "tokenize", "me" })]
    [InlineData("hitokenizeme", new[] { "hitokenizeme" })]
    [InlineData("", new string[0])]
    public void Tokenize_splits_on_whitespace(string input, string[] expected) =>
        Assert.Equal(expected, StringTokenizer.Tokenize(input));

    [Theory]
    [InlineData("hi tokenize\t\t  me", 2, new[] { "hi", "tokenize\t\t  me" })]
    [InlineData("hi tokenize me", 1, new[] { "hi tokenize me" })]
    [InlineData("hi tokenize\t\t  me", 100, new[] { "hi", "tokenize", "me" })]
    public void Tokenize_keeps_the_remainder_in_the_last_token(string input, int max, string[] expected) =>
        Assert.Equal(expected, StringTokenizer.Tokenize(input, max));

    [Theory]
    [InlineData("hi 'tokenize\t\t  me'", new[] { "hi", "tokenize\t\t  me" })]
    [InlineData("hi 'tokenize\t\t  \"me\"'", new[] { "hi", "tokenize\t\t  \"me\"" })]
    [InlineData("layout name 'Google Chrome':REPEAT push up", new[] { "layout", "name", "Google Chrome:REPEAT", "push", "up" })]
    public void Tokenize_keeps_quoted_whitespace(string input, string[] expected) =>
        Assert.Equal(expected, StringTokenizer.Tokenize(input, quoteChars: StringTokenizer.Quotes));

    [Fact]
    public void Tokenize_does_not_interpret_quotes_in_the_remainder() =>
        Assert.Equal(new[] { "layout", "n", "App", "shell 'a b'" },
            StringTokenizer.Tokenize("layout n 'App' shell 'a b'", 4, StringTokenizer.Quotes));

    [Theory]
    [InlineData("'iTerm'", "iTerm")]
    [InlineData("\"iTerm\"", "iTerm")]
    [InlineData("iTerm", "iTerm")]
    public void RemoveQuotes_strips_surrounding_quotes(string input, string expected) =>
        Assert.Equal(expected, StringTokenizer.RemoveQuotes(input));
}

public class StringDistanceTests
{
    [Theory]
    [InlineData("hello", "helo", 1)]
    [InlineData("hello", "hallo", 1)]
    [InlineData("hello", "helllo", 1)]
    [InlineData("hello", "hldla", 3)]
    [InlineData("Hello", "hello", 0)]
    public void Levenshtein(string a, string b, int expected) => Assert.Equal(expected, StringDistance.Levenshtein(a, b));

    [Theory]
    [InlineData("hello", "hell", 4)]
    [InlineData("hello", "hallo", 1)]
    [InlineData("hello", "ello", 0)]
    [InlineData("hello", "helllo", 4)]
    [InlineData("hello", "heldlo", 3)]
    public void Sequential(string a, string b, int expected) => Assert.Equal(expected, StringDistance.Sequential(a, b));
}
