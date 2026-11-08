using Slate.Core.Configuration;
using Slate.Core.Keyboard;

namespace Slate.Core.Tests.Keyboard;

public class KeystrokeTests
{
    private static readonly IKeyboardLayout Us = UsKeyboardLayout.Instance;
    private static Key K(int vk) => new(vk);

    [Theory]
    [InlineData("right:ctrl;alt", VirtualKeys.Right, Modifiers.Ctrl | Modifiers.Alt)]
    [InlineData("s:cmd,shift", VirtualKeys.LetterA + 18, Modifiers.Win | Modifiers.Shift)]
    [InlineData("pad1:win", VirtualKeys.Numpad0 + 1, Modifiers.Win)]
    [InlineData("f12", VirtualKeys.F1 + 11, Modifiers.None)]
    [InlineData("padEnter:alt", VirtualKeys.NumpadEnter, Modifiers.Alt)]
    [InlineData("delete:ctrl", VirtualKeys.Back, Modifiers.Ctrl)]
    [InlineData("`:alt", VirtualKeys.Oem3, Modifiers.Alt)]
    [InlineData("backslash:ctrl", VirtualKeys.Oem5, Modifiers.Ctrl)]
    public void Parses_key_and_modifiers(string text, int vk, Modifiers modifiers)
    {
        var parsed = BindingKeystroke.Parse(text, Us);
        Assert.Equal(new Keystroke(K(vk), modifiers), parsed.Keystroke);
        Assert.Null(parsed.ModalKeystroke);
    }

    [Theory]
    [InlineData("1:f4", Modifiers.None, false)]
    [InlineData("1:f4,ctrl,alt", Modifiers.Ctrl | Modifiers.Alt, false)]
    [InlineData("1:ctrl,f4:toggle", Modifiers.Ctrl, true)]
    public void Parses_modal_bindings(string text, Modifiers modalModifiers, bool toggle)
    {
        var parsed = BindingKeystroke.Parse(text, Us);
        Assert.Equal(new Keystroke(K(VirtualKeys.Digit0 + 1), Modifiers.None), parsed.Keystroke);
        Assert.Equal(new Keystroke(K(VirtualKeys.F1 + 3), modalModifiers), parsed.ModalKeystroke);
        Assert.Equal(toggle, parsed.Toggle);
    }

    [Theory]
    [InlineData("nokey:ctrl", "Unrecognized Key")]
    [InlineData("a:hyper", "Unrecognized Modifier")]
    [InlineData("a:fn", "Unsupported Modifier")]
    public void Rejects_unknown_keys_and_modifiers(string text, string title) =>
        Assert.Equal(title, Assert.Throws<ConfigException>(() => BindingKeystroke.Parse(text, Us)).Title);

    [Fact]
    public void Characters_resolve_through_the_keyboard_layout()
    {
        var azertyA = new SingleKeyLayout('a', K(0x51));
        Assert.Equal(K(0x51), BindingKeystroke.Parse("a:ctrl", azertyA).Keystroke.Key);
    }

    private sealed class SingleKeyLayout(char c, Key key) : IKeyboardLayout
    {
        public Key? KeyForCharacter(char ch) => ch == c ? key : null;
    }
}
