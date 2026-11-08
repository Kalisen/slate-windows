using Slate.Core.Configuration;

namespace Slate.Core.Keyboard;

/// <summary>A key pressed together with a set of modifiers.</summary>
public readonly record struct Keystroke(Key Key, Modifiers Modifiers)
{
    public override string ToString() =>
        Modifiers == Modifiers.None ? Key.ToString() : $"{Modifiers.ToString().Replace(", ", "+", StringComparison.Ordinal)}+{Key}";
}

/// <summary>
/// The keystroke part of a <c>bind</c> directive: <c>key:modifiers</c>, or <c>key:modal-key[,modifiers][:toggle]</c>
/// for a modal binding.
/// </summary>
public sealed record BindingKeystroke(Keystroke Keystroke, Keystroke? ModalKeystroke, bool Toggle)
{
    public const string ToggleSuffix = "toggle";

    public const string Ctrl = "ctrl";
    public const string Alt = "alt";
    public const string Shift = "shift";
    /// <summary>The macOS command key, mapped to the Windows key.</summary>
    public const string Cmd = "cmd";
    public const string Win = "win";
    /// <summary>The macOS fn key: not visible to Windows applications.</summary>
    public const string Fn = "fn";

    public static Modifiers ParseModifier(string name) => name.ToLowerInvariant() switch
    {
        Ctrl => Modifiers.Ctrl,
        Alt => Modifiers.Alt,
        Shift => Modifiers.Shift,
        Cmd or Win => Modifiers.Win,
        Fn => throw new ConfigException("Unsupported Modifier", $"The '{Fn}' modifier is not available on Windows"),
        _ => throw new ConfigException("Unrecognized Modifier", $"Unrecognized modifier '{name}'"),
    };

    public static BindingKeystroke Parse(string text, IKeyboardLayout layout)
    {
        string[] parts = text.Split(':');
        Key key = KeyNames.Parse(parts[0], layout)
            ?? throw new ConfigException("Unrecognized Key", $"Unrecognized key \"{parts[0]}\" in \"{text}\"");
        Modifiers modifiers = Modifiers.None;
        Key? modalKey = null;
        if (parts.Length >= 2 && parts[1].Length > 0)
        {
            foreach (string token in parts[1].Split([',', ';'], StringSplitOptions.RemoveEmptyEntries))
            {
                Key? asKey = KeyNames.Parse(token, layout);
                if (asKey is not null && !IsModifierName(token)) modalKey = asKey;
                else modifiers |= ParseModifier(token);
            }
        }
        bool toggle = parts.Length >= 3 && parts[2] == ToggleSuffix;
        return modalKey is { } modal
            ? new BindingKeystroke(new Keystroke(key, Modifiers.None), new Keystroke(modal, modifiers), toggle)
            : new BindingKeystroke(new Keystroke(key, modifiers), null, toggle);
    }

    /// <summary>Parses a plain <c>key:modifiers</c> keystroke, such as the <c>modalEscapeKey</c> config.</summary>
    public static Keystroke ParseSimple(string text, IKeyboardLayout layout)
    {
        var parsed = Parse(text, layout);
        return parsed.ModalKeystroke is null
            ? parsed.Keystroke
            : throw new ConfigException("Unrecognized Key", $"'{text}' must be a key with modifiers");
    }

    private static bool IsModifierName(string token) =>
        token.ToLowerInvariant() is Ctrl or Alt or Shift or Cmd or Win or Fn;
}
