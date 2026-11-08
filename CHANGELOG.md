# Changelog

## Unreleased

Windows 11 port of Slate (from the macOS fork at version 2.0.1).

- C# / .NET 10 tray application; self-contained single-file builds for x64 and ARM64.
- Same `.slate` config format, read from `%USERPROFILE%\.slate`: `config`, `alias`, `layout`, `default`, `bind`, `source`.
- All operations: `move`, `resize`, `push`, `nudge`, `throw`, `corner`, `chain`, `sequence`, `layout`, `focus`, `snapshot`,
  `activate-snapshot`, `delete-snapshot`, `hint`, `grid`, `switch`, `hide`, `show`, `toggle`, `shell`, `relaunch`, `undo`.
- `cmd` maps to the Windows key; `win` is accepted as well. `fn` is not available on Windows.
- Global keys use a low-level keyboard hook: repeat-on-hold, modal bindings and replacing Alt+Tab or Win+key shortcuts work.
- Window frames exclude the invisible resize borders of Windows 10/11; minimized and maximized windows are handled.
- `hide`/`show` minimize and restore every window of an application (Windows has no application hiding).
- Overlays (window hints, grid, switcher) are drawn with per-pixel transparency and scale with the display DPI.
- Config files must be owned by the user, an administrator or SYSTEM, and only they may write to them.
- Config changes require approval; the approved digest is stored with DPAPI in `%APPDATA%\Slate`.
- `shell` commands must be absolute Windows paths; quote paths with spaces with double quotes.
- `keyboardLayout` is accepted but ignored: keys follow the active Windows keyboard layout.
- Bug fixes over the macOS version: `down`/`below` screen references, `ordered:N` bounds, layout failure accounting,
  app-specific configs falling back to the global value, and `bar-resize`/`corner` expressions with operators.
