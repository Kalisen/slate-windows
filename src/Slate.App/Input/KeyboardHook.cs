using System.Runtime.InteropServices;
using Slate.App.Interop;
using Slate.Core.Keyboard;

namespace Slate.App.Input;

/// <summary>
/// A low-level keyboard hook feeding the <see cref="HotkeyDispatcher"/>. Unlike RegisterHotKey it sees key releases,
/// can take over Win+key and Alt+Tab, and can swallow keys only while an overlay is open.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    /// <summary>Marks input Slate injects so the hook ignores it.</summary>
    private const nint InjectedMarker = 0x534C4154; // "SLAT"

    private static readonly int[] ModifierKeys =
    [
        VirtualKeys.LShift, VirtualKeys.RShift, VirtualKeys.LControl, VirtualKeys.RControl,
        VirtualKeys.LMenu, VirtualKeys.RMenu, VirtualKeys.LWin, VirtualKeys.RWin,
    ];

    private readonly HotkeyDispatcher dispatcher;
    private readonly LowLevelKeyboardProc callback;
    private readonly HashSet<int> pressed = [];
    private Modifiers lastModifiers;
    private nint hook;

    public KeyboardHook(HotkeyDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        callback = OnKeyboardEvent;
    }

    public void Install()
    {
        if (hook != 0) return;
        hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, callback, NativeMethods.GetModuleHandle(null), 0);
        if (hook == 0) throw new InvalidOperationException($"Unable to install the keyboard hook (error {Marshal.GetLastPInvokeError()}).");
    }

    public void Dispose()
    {
        if (hook == 0) return;
        NativeMethods.UnhookWindowsHookEx(hook);
        hook = 0;
    }

    private nint OnKeyboardEvent(int code, nint wParam, nint lParam)
    {
        if (code < 0) return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
        var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        if (data.ExtraInfo == InjectedMarker) return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);

        bool swallow;
        try
        {
            swallow = Handle(data, isDown: (int)wParam is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN);
        }
        catch (Exception ex)
        {
            // An exception escaping a hook callback would kill the process; Windows also drops slow hooks.
            Log.Error($"Keyboard hook: {ex}");
            swallow = false;
        }
        return swallow ? 1 : NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
    }

    private bool Handle(KBDLLHOOKSTRUCT data, bool isDown)
    {
        int vk = data.VkCode;
        if (vk == VirtualKeys.Return && (data.Flags & NativeMethods.LLKHF_EXTENDED) != 0) vk = VirtualKeys.NumpadEnter;

        if (VirtualKeys.IsModifier(vk))
        {
            if (isDown) pressed.Add(vk); else pressed.Remove(vk);
            Modifiers held = HeldModifiers(vk);
            if (held != lastModifiers)
            {
                lastModifiers = held;
                dispatcher.OnModifiersChanged(held);
            }
            return false;
        }

        var key = new Key(vk);
        if (!isDown)
        {
            pressed.Remove(vk);
            return dispatcher.OnKeyUp(key);
        }

        bool isAutoRepeat = !pressed.Add(vk);
        Modifiers modifiers = HeldModifiers(vk);
        bool swallow = dispatcher.OnKeyDown(key, modifiers, isAutoRepeat);
        // Releasing Win or Alt after a swallowed chord would open the Start menu or the menu bar: mask it with a dummy key.
        if (swallow && !isAutoRepeat && (modifiers & (Modifiers.Win | Modifiers.Alt)) != 0) SendMaskKey();
        return swallow;
    }

    /// <summary>
    /// Modifiers held right now, from the key events seen by the hook. Other modifiers are cross-checked with the
    /// asynchronous key state to recover from releases the hook missed (e.g. while the secure desktop was shown); the
    /// key being processed is skipped because its asynchronous state is only updated after the hook returns.
    /// </summary>
    private Modifiers HeldModifiers(int currentVk)
    {
        Modifiers held = Modifiers.None;
        foreach (int vk in ModifierKeys)
        {
            if (vk != currentVk && pressed.Contains(vk) && (NativeMethods.GetAsyncKeyState(vk) & 0x8000) == 0)
                pressed.Remove(vk);
            if (pressed.Contains(vk)) held |= VirtualKeys.ModifierOf(vk);
        }
        return held;
    }

    private static void SendMaskKey()
    {
        var inputs = new INPUT[2];
        inputs[0].Type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].Keyboard = new KEYBDINPUT { Vk = VirtualKeys.Unassigned, ExtraInfo = InjectedMarker };
        inputs[1].Type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].Keyboard = new KEYBDINPUT { Vk = VirtualKeys.Unassigned, Flags = NativeMethods.KEYEVENTF_KEYUP, ExtraInfo = InjectedMarker };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }
}
