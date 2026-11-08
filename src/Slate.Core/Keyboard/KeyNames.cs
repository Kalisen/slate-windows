namespace Slate.Core.Keyboard;

/// <summary>Maps a printable character to the key that types it on the active keyboard layout.</summary>
public interface IKeyboardLayout
{
    Key? KeyForCharacter(char c);
}

/// <summary>The US layout, used by tests and when the system layout cannot type a character.</summary>
public sealed class UsKeyboardLayout : IKeyboardLayout
{
    public static readonly UsKeyboardLayout Instance = new();

    private static readonly Dictionary<char, int> Punctuation = new()
    {
        ['\''] = VirtualKeys.Oem7,
        [','] = VirtualKeys.OemComma,
        ['-'] = VirtualKeys.OemMinus,
        ['.'] = VirtualKeys.OemPeriod,
        ['/'] = VirtualKeys.Oem2,
        [';'] = VirtualKeys.Oem1,
        ['='] = VirtualKeys.OemPlus,
        ['`'] = VirtualKeys.Oem3,
        ['['] = VirtualKeys.Oem4,
        [']'] = VirtualKeys.Oem6,
        ['\\'] = VirtualKeys.Oem5,
    };

    public Key? KeyForCharacter(char c)
    {
        char lower = char.ToLowerInvariant(c);
        if (lower is >= 'a' and <= 'z') return new Key(VirtualKeys.LetterA + (lower - 'a'));
        if (lower is >= '0' and <= '9') return new Key(VirtualKeys.Digit0 + (lower - '0'));
        return Punctuation.TryGetValue(lower, out int vk) ? new Key(vk) : null;
    }
}

/// <summary>The key names accepted in bindings (see "Allowed Keys" in the README).</summary>
public static class KeyNames
{
    public const string Escape = "esc";
    public const string Backslash = "backslash";

    private static readonly Dictionary<string, int> Named = BuildNamed();

    private static Dictionary<string, int> BuildNamed()
    {
        var keys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [Backslash] = VirtualKeys.Oem5,
            ["caps"] = VirtualKeys.Capital,
            // "delete" is the macOS name of the backspace key.
            ["delete"] = VirtualKeys.Back,
            ["backspace"] = VirtualKeys.Back,
            ["forwarddelete"] = VirtualKeys.Delete,
            ["down"] = VirtualKeys.Down,
            ["end"] = VirtualKeys.End,
            [Escape] = VirtualKeys.Escape,
            // "help" sits where Windows keyboards have Insert.
            ["help"] = VirtualKeys.Insert,
            ["insert"] = VirtualKeys.Insert,
            ["home"] = VirtualKeys.Home,
            ["left"] = VirtualKeys.Left,
            ["mute"] = VirtualKeys.VolumeMute,
            ["pad*"] = VirtualKeys.Multiply,
            ["pad+"] = VirtualKeys.Add,
            ["pad-"] = VirtualKeys.Subtract,
            ["pad."] = VirtualKeys.Decimal,
            ["pad/"] = VirtualKeys.Divide,
            ["pad="] = VirtualKeys.OemNecEqual,
            ["padClear"] = VirtualKeys.Clear,
            ["padEnter"] = VirtualKeys.NumpadEnter,
            ["pageDown"] = VirtualKeys.Next,
            ["pageUp"] = VirtualKeys.Prior,
            ["return"] = VirtualKeys.Return,
            ["right"] = VirtualKeys.Right,
            ["space"] = VirtualKeys.Space,
            ["tab"] = VirtualKeys.Tab,
            ["up"] = VirtualKeys.Up,
        };
        for (int i = 0; i <= 9; i++) keys[$"pad{i}"] = VirtualKeys.Numpad0 + i;
        for (int i = 1; i <= 24; i++) keys[$"f{i}"] = VirtualKeys.F1 + i - 1;
        return keys;
    }

    /// <summary>Resolves a key name or single character, or returns null if the key is unknown.</summary>
    public static Key? Parse(string name, IKeyboardLayout layout)
    {
        if (Named.TryGetValue(name, out int vk)) return new Key(vk);
        return name.Length == 1 ? layout.KeyForCharacter(name[0]) ?? UsKeyboardLayout.Instance.KeyForCharacter(name[0]) : null;
    }

    public static string? NameOf(Key key)
    {
        foreach (var (name, vk) in Named)
        {
            if (vk == key.VirtualKey) return name;
        }
        if (key.VirtualKey is >= VirtualKeys.LetterA and < VirtualKeys.LetterA + 26) return ((char)('a' + key.VirtualKey - VirtualKeys.LetterA)).ToString();
        if (key.VirtualKey is >= VirtualKeys.Digit0 and < VirtualKeys.Digit0 + 10) return ((char)('0' + key.VirtualKey - VirtualKeys.Digit0)).ToString();
        return null;
    }
}
