namespace Slate.Core.Configuration;

/// <summary>Loads the user's config, falling back to the built-in default, and enforces user approval.</summary>
public sealed class ConfigLoader(ConfigParser parser, ConfigApproval approval, IConfigErrorReporter errors)
{
    public const string UserConfigPath = "~/.slate";
    private const string DefaultConfigResource = "Slate.Core.default.slate";

    /// <summary>The built-in configuration used when <c>~/.slate</c> cannot be loaded.</summary>
    public static string DefaultConfigText()
    {
        using Stream stream = typeof(ConfigLoader).Assembly.GetManifestResourceStream(DefaultConfigResource)
            ?? throw new InvalidOperationException($"Missing embedded resource {DefaultConfigResource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Throws <see cref="ConfigLoadAbortedException"/> when the user chooses to quit.</summary>
    public SlateConfiguration Load(string path = UserConfigPath)
    {
        var config = new SlateConfiguration();
        if (!parser.LoadFile(config, path, out ConfigFileResult result))
        {
            string reason = result.Status == ConfigFileStatus.Untrusted
                ? $"{result.Error}\n\nThe default configuration will be used."
                : $"{path} does not exist. The default configuration will be used.";
            if (errors.ReportAndAskQuit(new ConfigException($"Could not load {path}", reason))) throw new ConfigLoadAbortedException();
            var fallback = new SlateConfiguration { Source = ConfigSource.Default };
            parser.LoadText(fallback, DefaultConfigText());
            return fallback;
        }

        // Nothing from the user's config may run until it is approved.
        return approval.IsApproved(config.Review) ? config : new SlateConfiguration { Source = ConfigSource.Rejected };
    }
}
