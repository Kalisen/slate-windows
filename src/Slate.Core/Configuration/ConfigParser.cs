using Slate.Core.Keyboard;
using Slate.Core.Layouts;
using Slate.Core.Operations;
using Slate.Core.Screens;
using Slate.Core.Text;

namespace Slate.Core.Configuration;

/// <summary>The directives of a <c>.slate</c> file.</summary>
public enum Directive
{
    Config,
    Alias,
    Layout,
    Default,
    Bind,
    Source,
}

/// <summary>Reads <c>.slate</c> files top-down into a <see cref="SlateConfiguration"/>.</summary>
public sealed class ConfigParser(IConfigFileSource files, IKeyboardLayout keyboardLayout, string homeDirectory, IConfigErrorReporter errors)
{
    public const char CommentCharacter = '#';
    public const string SourceIfExists = "if_exists";
    public const string AliasOpen = "${";
    public const string AliasClose = "}";

    /// <summary>Each directive's name in a config file and its minimum token count, including the name.</summary>
    private static readonly Dictionary<string, (Directive Directive, int MinimumTokens)> Directives = new(StringComparer.Ordinal)
    {
        ["config"] = (Directive.Config, 3),
        ["alias"] = (Directive.Alias, 3),
        ["layout"] = (Directive.Layout, 4),
        ["default"] = (Directive.Default, 3),
        ["bind"] = (Directive.Bind, 3),
        ["source"] = (Directive.Source, 2),
    };

    private SlateConfiguration config = new();
    private ParseContext context = null!;

    /// <summary>Parses a file and everything it sources. Returns false if the file is missing or untrusted.</summary>
    public bool LoadFile(SlateConfiguration target, string path, out ConfigFileResult result)
    {
        config = target;
        context = new ParseContext(target.Settings, target.Layouts, keyboardLayout, homeDirectory);
        return Load(path, out result);
    }

    /// <summary>Parses configuration text that does not come from a file (the built-in default).</summary>
    public void LoadText(SlateConfiguration target, string text)
    {
        config = target;
        context = new ParseContext(target.Settings, target.Layouts, keyboardLayout, homeDirectory);
        Append(text);
    }

    /// <summary>Resolves a config path: relative paths are relative to the home directory and <c>~</c> is expanded.</summary>
    public string ResolvePath(string file)
    {
        string path = file.StartsWith('~') || Path.IsPathRooted(file) ? file : "~/" + file;
        return Path.GetFullPath(ParseContext.ExpandHome(path, homeDirectory));
    }

    private bool Load(string file, out ConfigFileResult result)
    {
        string path = ResolvePath(file);
        result = files.Read(path);
        if (result.Status != ConfigFileStatus.Loaded) return false;
        config.Review.AddFile(path, result.Contents!);
        Append(result.Contents!);
        return true;
    }

    private void Append(string text)
    {
        foreach (string rawLine in text.Split('\n'))
        {
            string? line = StripComments(rawLine);
            if (string.IsNullOrEmpty(line)) continue;
            try
            {
                line = ReplaceAliases(line);
                config.Review.AddLine(line);
                ParseLine(line);
            }
            catch (ConfigException ex)
            {
                if (errors.ReportAndAskQuit(ex)) throw new ConfigLoadAbortedException();
            }
        }
        config.UsesUndo |= context.UsesUndo;
    }

    private void ParseLine(string line)
    {
        var tokens = StringTokenizer.Tokenize(line);
        if (!Directives.TryGetValue(tokens[0], out var spec) || tokens.Count < spec.MinimumTokens) return;

        switch (spec.Directive)
        {
            case Directive.Config:
                SetConfig(tokens[1], tokens[2]);
                break;
            case Directive.Bind:
                config.Bindings.Add(Binding.Parse(line, context));
                break;
            case Directive.Layout:
                if (!config.Layouts.TryGetValue(tokens[1], out Layout? layout)) layout = new Layout(tokens[1]);
                layout.Add(line, context);
                config.Layouts[tokens[1]] = layout;
                break;
            case Directive.Default:
                config.Defaults.Add(DefaultLayout.Parse(line));
                break;
            case Directive.Alias:
                var aliasTokens = StringTokenizer.Tokenize(line, 3);
                config.Aliases[AliasOpen + aliasTokens[1] + AliasClose] = aliasTokens[2];
                break;
            case Directive.Source:
                if (!Load(tokens[1], out ConfigFileResult result) && !(tokens.Count >= 3 && tokens[2] == SourceIfExists))
                {
                    string reason = result.Status == ConfigFileStatus.Untrusted ? result.Error! : "The file does not exist.";
                    throw new ConfigException($"Error Sourcing File '{tokens[1]}'", reason);
                }
                break;
        }
    }

    /// <summary><c>config key value</c>, or <c>config key:'App Name' value</c> for an application-specific value.</summary>
    private void SetConfig(string keyToken, string value)
    {
        int colon = keyToken.IndexOf(':', StringComparison.Ordinal);
        string key = colon < 0 ? keyToken : keyToken[..colon];
        if (!SlateSettings.IsKnown(key)) throw new ConfigException($"Unrecognized Config '{keyToken}'", $"Unrecognized config '{keyToken}'");
        string app = colon < 0 ? string.Empty : StringTokenizer.RemoveQuotes(keyToken[(colon + 1)..]);
        if (app.Length > 0) config.Settings.SetForApp(key, app, value);
        else config.Settings.Set(key, value);
    }

    public static string? StripComments(string line)
    {
        string trimmed = line.Trim(' ', '\t', '\r');
        if (trimmed.Length == 0 || trimmed[0] == CommentCharacter) return null;
        int comment = trimmed.IndexOf(CommentCharacter, StringComparison.Ordinal);
        return comment < 0 ? trimmed : trimmed[..comment].Trim(' ', '\t');
    }

    private string ReplaceAliases(string line)
    {
        foreach (var (name, value) in config.Aliases) line = line.Replace(name, value, StringComparison.Ordinal);
        if (line.Contains(AliasOpen, StringComparison.Ordinal))
            throw new ConfigException("Unrecognized Alias", $"Unrecognized alias in '{line}'");
        return line;
    }
}
