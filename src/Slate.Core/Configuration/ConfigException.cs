namespace Slate.Core.Configuration;

/// <summary>A configuration or operation error shown to the user with a short <see cref="Title"/>.</summary>
public sealed class ConfigException(string title, string message) : Exception(message)
{
    public string Title { get; } = title;
}
