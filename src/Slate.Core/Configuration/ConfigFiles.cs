namespace Slate.Core.Configuration;

public enum ConfigFileStatus
{
    Loaded,
    Missing,
    /// <summary>The file exists but is refused, e.g. because other users can modify it.</summary>
    Untrusted,
}

public sealed record ConfigFileResult(ConfigFileStatus Status, string? Contents, string? Error)
{
    public static ConfigFileResult Loaded(string contents) => new(ConfigFileStatus.Loaded, contents, null);
    public static readonly ConfigFileResult Missing = new(ConfigFileStatus.Missing, null, null);
    public static ConfigFileResult Untrusted(string error) => new(ConfigFileStatus.Untrusted, null, error);
}

/// <summary>Reads config files, refusing those that other users could have modified.</summary>
public interface IConfigFileSource
{
    ConfigFileResult Read(string path);
}

/// <summary>Reports a config error; returns true if the user chose to quit Slate.</summary>
public interface IConfigErrorReporter
{
    bool ReportAndAskQuit(ConfigException error);
}

/// <summary>Thrown when the user chooses to quit Slate while the config is being loaded.</summary>
public sealed class ConfigLoadAbortedException() : Exception("The user quit while loading the configuration.");
