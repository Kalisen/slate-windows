using Slate.Core.Keyboard;
using Slate.Core.Layouts;
using Slate.Core.Screens;

namespace Slate.Core.Configuration;

/// <summary>Where the active configuration came from.</summary>
public enum ConfigSource
{
    /// <summary>The user's <c>~/.slate</c> and the files it sources.</summary>
    User,
    /// <summary>The built-in default config, used when <c>~/.slate</c> is missing or untrusted.</summary>
    Default,
    /// <summary>The user did not approve the changed config: nothing is bound.</summary>
    Rejected,
}

/// <summary>A parsed configuration.</summary>
public sealed class SlateConfiguration
{
    public SlateSettings Settings { get; } = new();
    public KeyBindings Bindings { get; } = new();
    public Dictionary<string, Layout> Layouts { get; } = new(StringComparer.Ordinal);
    public List<DefaultLayout> Defaults { get; } = [];
    public Dictionary<string, string> Aliases { get; } = new(StringComparer.Ordinal);
    public ConfigReview Review { get; } = new();

    /// <summary>True when some binding uses <c>undo</c>: undoable operations then record snapshots.</summary>
    public bool UsesUndo { get; set; }

    public ConfigSource Source { get; set; } = ConfigSource.User;
}
