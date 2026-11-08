namespace Slate.Core.Keyboard;

/// <summary>Keyboard modifiers. <c>cmd</c> in configs maps to <see cref="Win"/>.</summary>
[Flags]
public enum Modifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>A physical key identified by its Windows virtual-key code.</summary>
public readonly record struct Key(int VirtualKey)
{
    public override string ToString() => KeyNames.NameOf(this) ?? $"vk{VirtualKey:X2}";
}

/// <summary>Windows virtual-key codes used by Slate.</summary>
public static class VirtualKeys
{
    public const int Back = 0x08;
    public const int Tab = 0x09;
    public const int Clear = 0x0C;
    public const int Return = 0x0D;
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Menu = 0x12;
    public const int Capital = 0x14;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int Prior = 0x21;
    public const int Next = 0x22;
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int Insert = 0x2D;
    public const int Delete = 0x2E;
    public const int Digit0 = 0x30;
    public const int LetterA = 0x41;
    public const int LWin = 0x5B;
    public const int RWin = 0x5C;
    public const int Numpad0 = 0x60;
    public const int Multiply = 0x6A;
    public const int Add = 0x6B;
    public const int Subtract = 0x6D;
    public const int Decimal = 0x6E;
    public const int Divide = 0x6F;
    public const int F1 = 0x70;
    public const int LShift = 0xA0;
    public const int RShift = 0xA1;
    public const int LControl = 0xA2;
    public const int RControl = 0xA3;
    public const int LMenu = 0xA4;
    public const int RMenu = 0xA5;
    public const int VolumeMute = 0xAD;
    public const int Oem1 = 0xBA;      // ;:
    public const int OemPlus = 0xBB;   // =+
    public const int OemComma = 0xBC;  // ,<
    public const int OemMinus = 0xBD;  // -_
    public const int OemPeriod = 0xBE; // .>
    public const int Oem2 = 0xBF;      // /?
    public const int Oem3 = 0xC0;      // `~
    public const int Oem4 = 0xDB;      // [{
    public const int Oem5 = 0xDC;      // \|
    public const int Oem6 = 0xDD;      // ]}
    public const int Oem7 = 0xDE;      // '"
    public const int OemNecEqual = 0x92; // numpad =

    /// <summary>Unassigned code injected to stop Windows from treating a swallowed Win/Alt chord as a lone Win/Alt tap.</summary>
    public const int Unassigned = 0xE8;

    /// <summary>
    /// Synthetic code for the numpad Enter key, which shares <see cref="Return"/> and is told apart by the extended flag.
    /// </summary>
    public const int NumpadEnter = 0x10D;

    public static bool IsModifier(int vk) =>
        vk is Shift or Control or Menu or LShift or RShift or LControl or RControl or LMenu or RMenu or LWin or RWin;

    public static Modifiers ModifierOf(int vk) => vk switch
    {
        Shift or LShift or RShift => Modifiers.Shift,
        Control or LControl or RControl => Modifiers.Ctrl,
        Menu or LMenu or RMenu => Modifiers.Alt,
        LWin or RWin => Modifiers.Win,
        _ => Modifiers.None,
    };
}
