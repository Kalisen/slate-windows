# Slate for Windows

A Windows 11 port of [Slate](https://github.com/Kalisen/slate), the keyboard-driven, highly configurable window manager
originally written for macOS. Your `.slate` config works on Windows, with a few platform differences listed below.

## Summary of Features

* Highly customizable
* Bind keystrokes to:
  * move and/or resize windows
  * directionally focus windows
  * activate preset layouts
  * create, delete, and activate snapshots of the current state of windows
* Set default layouts for different monitor configurations which will activate when that configuration is detected.
* Window Hints: an intuitive way to change window focus
* A grid to resize windows with the mouse
* A customizable application switcher that can replace Alt+Tab

## Credits

Slate was created by [Jigish Patel](https://github.com/jigish/slate). Big thanks to [philc](https://github.com/philc) for
the Window Hints idea (and initial implementation) as well as plenty of other suggestions and improvement ideas.

# Using Slate

## Installing Slate

Requirements: Windows 11 (Windows 10 1809 or later should work), x64 or ARM64. The builds are self-contained: no .NET
runtime is needed.

### Download

Download `Slate-X.Y.Z-win-x64.zip` (or `-win-arm64`) from
[GitHub Releases](https://github.com/Kalisen/slate-windows/releases), extract `Slate.exe` to a folder of your choice
(e.g. `%LOCALAPPDATA%\Programs\Slate`) and run it. Releases are not code signed: SmartScreen may ask you to confirm the
first launch.

Slate lives in the notification area. Its menu offers:

* **About Slate**
* **Relaunch and Load Config**: restart Slate and reload `.slate`
* **Launch Slate on Login**
* **Take Snapshot** / **Activate Snapshot**: save and restore the position of every window
* **Current Window Info**: screen ids and window positions, in config terms, to help write layouts
* **Quit Slate**

### Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

    git clone https://github.com/Kalisen/slate-windows.git
    cd slate-windows
    dotnet test
    dotnet publish src/Slate.App/Slate.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish

`publish\Slate.exe` is the application.

### Elevated windows

Windows prevents a normal process from controlling or listening to administrator windows. While an elevated window
(e.g. an administrator terminal) has the focus, Slate's bindings do not fire and Slate cannot move it. Run Slate as
administrator to manage those windows too.

## Configuring Slate

Slate is configured using a `.slate` file in your user profile (`%USERPROFILE%\.slate`, e.g. `C:\Users\you\.slate`). If
it does not exist, Slate offers to use the [default config](src/Slate.Core/Configuration/default.slate).

Slate asks you to approve your config whenever `.slate` or a file it `source`s changes. The dialog lists the files and
every line that runs a `shell` command; approve only changes you made. If you don't approve, Slate runs without bindings
or layouts until the next relaunch. Approvals are stored in `%APPDATA%\Slate`, encrypted for your Windows account.

Config files (`.slate` and any file it `source`s) must be owned by you (or an administrator or SYSTEM) and only you,
administrators and SYSTEM may modify them; otherwise Slate refuses to load them. Files in your profile have those
permissions by default. If Slate reports that a file "can be modified by" someone else, remove that permission, for
example `icacls "%USERPROFILE%\.slate" /remove "Everyone"`. Symbolic links are followed and the target is checked.

Configuration is loaded when Slate starts. To apply config changes, use "Relaunch and Load Config" in the menu or bind
the `relaunch` operation.

Configuration is split into the following directives:

* `config` (for global configurations)
* `alias` (to create alias variables)
* `layout` (to configure layouts)
* `default` (to default certain screen configurations to layouts)
* `bind` (for key bindings)
* `source` (to load configs from another file)

**Note:** `#` is the comment character. Anything after a `#` will be ignored.

### Differences with macOS

| macOS | Windows |
|:------|:--------|
| `cmd` modifier | The Windows key. `win` is accepted too. |
| `fn` modifier | Not available: Windows applications cannot see it. |
| `~/.slate` | `%USERPROFILE%\.slate`; `~` expands to `%USERPROFILE%` everywhere. |
| Application names (`'iTerm'`) | The program's description (as shown in Task Manager, e.g. `'Google Chrome'`) or its executable name (`'chrome'` or `'chrome.exe'`), case-insensitive. Use "Current Window Info" to see the names. |
| `hide` / `show` | Minimize / restore every window of the application. |
| `shell '/usr/bin/open'` | `shell 'C:\Windows\explorer.exe'`. Quote paths with spaces with double quotes: `shell '"C:\Program Files\App\app.exe" --flag'`. |
| `keyboardLayout` config | Ignored: keys follow the active Windows keyboard layout. |
| Coordinates in points | Coordinates are physical pixels. Hint, grid and switcher sizes are scaled with the display's scale factor. |
| Binding cmd-tab replaces the app switcher | Binding `tab:alt` (or `tab:cmd`, i.e. Win+Tab) to `switch` replaces that shortcut. |

Windows reserves a few shortcuts that no application can take over, such as Win+L and Ctrl+Alt+Del. Other Win+key
shortcuts (e.g. Win+Arrow for Snap) are replaced while a binding uses them.

#### Porting a macOS `.slate` file

`cmd` maps to the Windows key by name, but on a PC keyboard the key next to the space bar is Alt, where `cmd` sits on a
Mac. To keep the same finger positions as on macOS, swap the two modifiers: write `alt` where the Mac file says `cmd`,
and `win` where it says `alt` (option). [examples/mac-keyboard-layout.slate](examples/mac-keyboard-layout.slate) is a
complete configuration converted this way.

### Expressions

Some directives allow parameters that can be expressions. The following strings will be replaced with the appropriate values when using expressions:

    screenOriginX = target screen's top left x coordinate (should not be used in Window Hints configs)
    screenOriginY = target screen's top left y coordinate (should not be used in Window Hints configs)
    screenSizeX = target screen's width
    screenSizeY = target screen's height
    windowTopLeftX = window's current top left x coordinate (should not be used in Window Hints configs)
    windowTopLeftY = window's current top left y coordinate (should not be used in Window Hints configs)
    windowSizeX = window's width
    windowSizeY = window's height
    newWindowSizeX = window's new width (after resize, only usable in topLeftX and topLeftY, should not be
                     used in configs)
    newWindowSizeY = window's new height (after resize, only usable in topLeftX and topLeftY, should not be
                     used in configs)
    windowHintsWidth = the value of the windowHintsWidth config (only usable in windowHintsTopLeftX and
                       windowHintsTopLeftY)
    windowHintsHeight = the value of the windowHintsHeight config (only usable in windowHintsTopLeftX and
                        windowHintsTopLeftY)

The screen size and origin are those of the screen's work area (the screen minus the taskbar). In addition to the
variables above, expressions can be used with the following functions and operators:

    +          e.g. 1+1 = 2
    -          e.g. 1-1 = 0
    *          e.g. 2*2 = 4
    /          e.g. 4/2 = 2
    **         e.g. 3**2 = 9
    sum        e.g. sum({1,2,3}) = 6
    count      e.g. count({4,5,6}) = 3
    min        e.g. min({1,3,5}) = 1
    max        e.g. max({1,3,5}) = 5
    average    e.g. average({1,2,3,4}) = 2.5
    median     e.g. median({1,2,3,10,15}) = 3
    stddev     e.g. stddev({1,2,3,4,5}) = 1.4142135623730951
    sqrt       e.g. sqrt(9) = 3.0
    log        e.g. log(100) = 2.0
    ln         e.g. ln(8) = 2.0794415416798357
    exp        e.g. exp(2) = 7.3890560989306504 (this is "e**parameter")
    floor      e.g. floor(1.9) = 1.0
    ceiling    e.g. ceiling(1.1) = 2.0
    abs        e.g. abs(-1) = 1
    trunc      e.g. trunc(1.1123123123) = 1.0
    random     e.g. random() = 0.20607629744336009 (random float between 0 and 1)
    randomn    e.g. randomn(10) = 4 (random integer between 0 and parameter-1)

**Note:** When using expressions spaces are *not* allowed!

### The `config` Directive

The `config` directive follows the following format:

    config name value

Example:

    config defaultToCurrentScreen true

A config can apply to one application only: `config snapshotTitleMatch:'Code' sequential`.

**Note:** the `.slate` file is read top-down: directives that come before `config` directives may not have the `config`
applied. As such, it is best to put `config` directives at the top of your `.slate` file.

Allowed configs and their defaults:

| Name | Default | Meaning |
|:-----|:--------|:--------|
| `defaultToCurrentScreen` | `false` | Use the current screen when a referenced screen does not exist |
| `nudgePercentOf` | `screenSize` | What `nudge` percentages are relative to: `screenSize` or `windowSize` |
| `resizePercentOf` | `screenSize` | What `resize` percentages are relative to: `screenSize` or `windowSize` |
| `repeatOnHoldOps` | `resize,nudge` | Operations that repeat while their keys are held |
| `secondsBeforeRepeat` | `0.4` | Delay before a held binding repeats |
| `secondsBetweenRepeat` | `0.2` | Delay between repeats |
| `checkDefaultsOnLoad` | `false` | Apply `default` directives when Slate starts |
| `focusCheckWidth` | `100` | Width of the area `focus` searches next to the window |
| `focusCheckWidthMax` | `100` | `focus` widens its search by `focusCheckWidth` up to this width |
| `focusPreferSameApp` | `true` | `focus` prefers windows of the focused application |
| `orderScreensLeftToRight` | `true` | Number screens left to right instead of in system order |
| `windowHintsFontName` | `Segoe UI` | |
| `windowHintsFontSize` | `40` | |
| `windowHintsFontColor` | `255;255;255;1.0` | Colors are `red;green;blue;alpha` |
| `windowHintsWidth` | `100` | |
| `windowHintsHeight` | `100` | |
| `windowHintsBackgroundColor` | `50;53;58;0.9` | |
| `windowHintsDuration` | `3` | Seconds before hints disappear |
| `windowHintsRoundedCornerSize` | `5` | |
| `windowHintsIgnoreHiddenWindows` | `true` | Skip windows whose hint would be covered by another window |
| `windowHintsTopLeftX` | `(windowSizeX/2)-(windowHintsWidth/2);0` | Semicolon separated candidate positions, relative to the window |
| `windowHintsTopLeftY` | `(windowSizeY/2)-(windowHintsHeight/2);0` | |
| `windowHintsOrder` | `leftToRight` | `none`, `persist` (a window keeps its hint), `leftToRight` or `rightToLeft` |
| `windowHintsShowIcons` | `false` | |
| `windowHintsIconAlpha` | `1.0` | |
| `windowHintsSpread` | `false` | Move overlapping hints apart |
| `windowHintsSpreadSearchWidth` | `40` | |
| `windowHintsSpreadSearchHeight` | `40` | |
| `windowHintsSpreadPadding` | `20` | |
| `switchIconSize` | `100` | |
| `switchIconPadding` | `5` | |
| `switchBackgroundColor` | `50;53;58;0.3` | |
| `switchSelectedBackgroundColor` | `50;53;58;0.9` | |
| `switchSelectedBorderColor` | `230;230;230;0.9` | |
| `switchSelectedBorderSize` | `2` | |
| `switchRoundedCornerSize` | `5` | |
| `switchOrientation` | `horizontal` | `horizontal` or `vertical` |
| `switchSecondsBeforeRepeat` | `0.3` | |
| `switchSecondsBetweenRepeat` | `0.03` | |
| `switchStopRepeatAtEdge` | `true` | |
| `switchOnlyFocusMainWindow` | `true` | Bring only the main window forward instead of all the application's windows |
| `switchFontSize` | `14` | |
| `switchFontColor` | `255;255;255;1.0` | |
| `switchFontName` | `Segoe UI` | |
| `switchShowTitles` | `false` | |
| `switchType` | `app` | |
| `switchSelectedPadding` | `10` | |
| `keyboardLayout` | `qwerty` | Ignored on Windows |
| `snapshotTitleMatch` | `levenshtein` | How windows are matched to saved ones: `levenshtein` or `sequential` |
| `snapshotMaxStackSize` | `0` | 0 means unlimited |
| `undoMaxStackSize` | `10` | |
| `undoOps` | `activate-snapshot,chain,grid,layout,move,push,nudge,corner,throw,resize,sequence,shell` | Operations `undo` can revert |
| `gridBackgroundColor` | `75;77;81;1.0` | |
| `gridRoundedCornerSize` | `5` | |
| `gridCellBackgroundColor` | `100;106;116;0.8` | |
| `gridCellSelectedColor` | `50;53;58;0.8` | |
| `gridCellRoundedCornerSize` | `5` | |
| `layoutFocusOnActivate` | `false` | Focus each window a layout moves, in layout order |
| `modalEscapeKey` | (none) | A keystroke that ends modal mode, e.g. `esc` |

### The `alias` Directive

The `alias` directive follows the following format:

    alias name value

When you set an alias, you can refer to it in any directive (sequentially after that alias directive) by referencing like `${name}`.

Example:

    alias bot-right-2nd-mon move screenOriginX+2*screenSizeX/3;screenOriginY+screenSizeY/2 screenSizeX/3;screenSizeY/2 1

Will allow you to use `${bot-right-2nd-mon}` as a reference to `move screenOriginX+2*screenSizeX/3;screenOriginY+screenSizeY/2 screenSizeX/3;screenSizeY/2 1` in any directive following the alias (including other alias directives)

### The `layout` Directive

The `layout` directive follows the following format:

    layout name 'app name':OPTIONS operations

Where:

    name = the name you want to use to reference the layout
    'app name' = single-quoted name of the application to add to the layout **or** BEFORE or AFTER
    OPTIONS = a comma separated list of options for this application (cannot be used with BEFORE or AFTER)
    operations = a pipe separated list of operations (move, resize, push, nudge, throw, or corner)

Possible Options:

| Name | Function |
|:-----|:---------|
| `IGNORE_FAIL` | This will let slate move to the next operation if the current operation fails to resize/move on the current window |
| `REPEAT` | This will repeat the list of operations if the number of windows is larger than the number of operations |
| `REPEAT_LAST` | This will repeat the last operation in the list if the number of windows is larger than the number of operations |
| `MAIN_FIRST` | This will cause the main window to always use the first operation |
| `MAIN_LAST` | This will cause the main window to always use the last operation (mutally exclusive with `MAIN_FIRST`) |
| `SORT_TITLE` | This will cause the window operations to be triggered on the windows in sorted order by the window title (can be used with `MAIN_FIRST` or `MAIN_LAST`) |
| `TITLE_ORDER=order` | This will cause the operations to be triggered on the windows starting with order which is a semi-colon separated list of window titles |
| `TITLE_ORDER_REGEX=order` | This will cause the operations to be triggered on the windows starting with the order which is a semi-colon separated list of window title regexes to match. Note that once a match is seen, the next regex will be used to match. This means if you have two windows that match the same regex, only the first one seen will be matched. The second will not. |

The main window of an application is its front-most window.

You can have multiple layout directives that point to the same name in order to link any number of applications to the same layout.

Example:

    layout myLayout 'Windows Terminal' push up bar-resize:screenSizeY/2 | push down bar-resize:screenSizeY/2
    layout myLayout 'Google Chrome' push left bar-resize:screenSizeX/2 | push right bar-resize:screenSizeX/2
    layout myLayout BEFORE shell path:~/ 'C:\Windows\System32\notepad.exe'
    layout myLayout AFTER shell path:~/ 'C:\tools\after.exe'

Will create a layout called `myLayout` with two operations for Windows Terminal and two operations for Google Chrome. When activated, the first window of Windows Terminal will be moved using the first operation in the first list and the second window using the second operation in the first list. In addition, the first window of Google Chrome will be moved using the first operation in the second list and the second window of Google Chrome will be moved using the second operation in the second list. Finally, the `BEFORE` operation will be run before any applications are moved and the `AFTER` operation will be run after. BEFORE and AFTER may also be used if the layout doesn't have any applications tied to it. Also, you may specify multiple BEFORE or AFTER lines (they will be run in the order that they appear). More information on how to actually use these layouts can be found under the `layout` operation in the `bind` directive section.

### The `default` Directive

The `default` directive follows the following format (tokens may be separated by any number of spaces):

    default layout-or-snapshot-name screen-configuration

Where:

    layout-or-snapshot-name = the name of the layout or snapshot you want to default to
    screen-configuration = either "count:NUMBER_OF_SCREENS" or
                                  "resolutions:SEMICOLON_SEPARATED_LIST_OF_RESOLUTIONS"

This directive will cause any screen configuration change (add monitor, remove monitor, screen resolution change) to trigger a search for a default layout or snapshot. If the screen configuration matches one of the defaults set, the layout or snapshot matching `layout-or-snapshot-name` will be triggered. For example:

    default myLayout count:2

Will trigger `myLayout` anytime the screen configuration changes to have 2 monitors. Also:

    default myLayout2 resolutions:1440x900;1024x768;1680x1050

Will trigger `myLayout2` anytime the screen configuration changes to have exactly 3 monitors with resolutions `1440x900`, `1024x768`, and `1680x1050`.

### The `bind` Directive

The `bind` directive follows one of the following formats (tokens may be separated by any number of spaces):

    bind key:modifiers operation parameter+
    bind key:modal-key operation parameter+

#### Key

`key` is a reference to a key on the keyboard. See Allowed Keys for a complete list. For example: the `s` key would simply be `s` while the `1` key on the number pad would be `pad1`.

#### Modifiers

`modifiers` is a comma or semicolon separated list of standard modifier keys. Allowed modifiers are:

* Control: `ctrl`
* Alt: `alt`
* Windows key: `win` or `cmd`
* Shift: `shift`

**Note:** If you bind `tab:alt` to `switch`, Slate replaces the Windows application switcher (Alt+Tab).

#### Modal Key

`modal-key` is any one of the Allowed Keys. If using a `modal-key`, pressing that key will cause the Slate notification area icon to change indicating modal mode is activated. then clicking `key` will activate the binding. Modal mode will remain active until `key` has been pressed or `modal-key` is pressed again. You may specify multiple bindings with the same `modal-key` as long as `key` is different. Also, `modal-key` can accompany a comma or semicolon separated list of modifier keys listed above. This will cause that entire keystroke to be considered the modal activation binding. For example: `bind 1:f4,ctrl,alt` will result in the modal keystroke being `ctrl+alt+f4`. After pressing that keystroke, modal mode will be activated and pressing `1` after that will activate the binding.

##### Modal Toggle Behavior

If you add `:toggle` to the end of a modal binding it will cause that binding to not end the modal mode. For example with the binding `1:ctrl,f4`, you press `ctrl+f4` and then press `1` to activate the binding. Once that binding is activated, modal mode will end and you have to press `ctrl+f4` again to activate it. However, with the binding `1:ctrl,f4:toggle` pressing `ctrl+f4` will toggle modal mode. pressing `1` will activate the binding but not end modal mode. To end modal mode, press `ctrl+f4` again or use the config `modalEscapeKey`.

#### Operation

Operations define what to actually do to the focused window.

**Screens**

Some operations allow you to specify a screen. Here are the list of possible values for screen:

* Integer representing the screen ID (indexed at 0). Screens are ordered from left to right (by X coordinate of the origin which is the top-left point). If `orderScreensLeftToRight` is set to false, the screen ID is the system ID (indexed at 0, the primary screen is 0). If `orderScreensLeftToRight` is set to false but you still want to reference screens in the default ordered mode, prefix the screen ID with `ordered:`.
* Screen resolution in the format `WIDTHxHEIGHT` (e.g. `1440x900`)
* Screen direction relative to the current screen (`left|right|up|above|down|below`)
* `next` or `previous` (represents the `currentID+1` or `currentID-1` screen)

**Allowed operations are:**

##### move
Move/Resize the window any which way: `move topLeftX;topLeftY sizeX;sizeY screen`

        topLeftX = top left x coordinate of the window's desired position (can be an expression)
        topLeftY = top left y coordinate of the window's desired position (can be an expression)
        sizeX = width of the window's desired position (can be an expression)
        sizeY = height of the window's desired position (can be an expression)
        screen = (optional) the reference to the screen of the window's desired position.
                 If this is not specified, it will default to the screen the window is currently on.
                 See the table at the beginning of the Operation section for more information.

    Example:

        bind pad1:ctrl move 0;0 100;100 1

    Will bind the keystroke ctrl-numpad1 to moving the window to the screen at index `1` with top-left coordinate `0,0` and size `100,100`

    **Note:** Remember to offset with `screenOriginX` in your `topLeftX` and `screenOriginY` in your `topLeftY` when using the `screen` option (or when using multiple screens in general) or your move operation will offset from the default origin `(0,0)` which is the origin of screen `0`.

##### resize
Resize the window (keeping top-left the same): `resize x y anchor`

        x = amount to resize width either as a percent or a hard value (+10% or -100)
        y = amount to resize height either as a percent or a hard value (+10% or -100)
        anchor = (optional) which corner to anchor on top-left|top-right|bottom-left|bottom-right (default is top-left)

    Example:

        bind right:ctrl resize +10% +0

    Will bind the keystroke ctrl-rightarrow to increase the width the current window by `10%`.

##### push
Push the window to the edge of the screen: `push direction style screen`

        direction = top|up|bottom|down|left|right
        style = (optional) none|center|bar|bar-resize:expression (default is none)
        screen = (optional) the reference to the screen of the window's desired position.
                 If this is not specified, it will default to the screen the window is currently on.
                 See the table at the beginning of the Operation section for more information.

    Example:

        bind up:alt,ctrl push up

    Will bind the keystroke alt-ctrl-uparrow to push the window so that it is aligned with the top of the screen

##### nudge
Nudge the window in any direction: `nudge x y`

        x = amount to nudge x either as a percent or a hard value (+10% or -100)
        y = amount to nudge y either as a percent or a hard value (+10% or -100)

    Example:

        bind left:ctrl,shift nudge -100 +0

    Will bind the keystroke ctrl-shift-leftarrow to nudge the window `100` pixels to the left

##### throw
Throw the window to any screen's origin: `throw screen style`

        screen = the screen you want to throw the window to (0 indexed)
        style = (optional) resize|noresize|resize:x-expression;y-expression (default will not resize)

    Example:

        bind pad1:alt,ctrl throw 1 resize

    Will bind the keystroke alt-ctrl-numpad1 to throw the window to the 2nd screen and resize it to fit that screen

##### corner
Move/Resize the window into a corner: `corner direction style screen`

        direction = top-left|top-right|bottom-left|bottom-right
        style = (optional) resize:x-expression;y-expression (default will not resize)
        screen = (optional) the reference to the screen of the window's desired position.
                 If this is not specified, it will default to the screen the window is currently on.
                 See the table at the beginning of the Operation section for more information.

    Example:

        bind 1:ctrl corner top-left resize:screenSizeX/2;screenSizeY/2

    Will bind the keystroke ctrl-1 to move the window to the top-left corner and resize it to 1/4 of the screen

##### shell
Execute a program: `shell options 'command'`

        command = (required) the program to run and its arguments. note that it is a quoted string. The program must be
                  an absolute path (e.g. 'C:\Windows\explorer.exe', not 'explorer'); '~\' is expanded. Use double
                  quotes around a path with spaces: '"C:\Program Files\App\app.exe" --flag'.
        options = (optional) a space separated list of:
                   wait  - block slate until the program exits. Useful when using shell commands in a
                           sequence binding
                   path: - the inital working directory to use when starting the command. For example
                           path:~/code would set the inital working directory to %USERPROFILE%\code

    Example:

        bind 1:ctrl shell wait path:~/code 'C:\Windows\System32\notepad.exe'

    Will bind the keystroke ctrl-1 to run Notepad with the current working directory of `%USERPROFILE%\code`. Slate will
    also block until Notepad exits. To run a command of `cmd.exe` or PowerShell, run the interpreter itself:
    `'C:\Windows\System32\cmd.exe /c start https://example.com'`.

##### hide
Hide (minimize) one or more applications: `hide applications`

        applications = a comma separated list of application names. Individual application names must be
                       surrounded by quotes. You can also specify `current`, `all`, or `all-but:` for the
                       Application name (no quotes). `current` will apply to the currently focused
                       application, `all` will apply to all open applications and `all-but:'APP_NAME'` will
                       apply to all open applications except `APP_NAME`.

    Example:

        bind 1:ctrl hide 'Windows Terminal','Google Chrome'

    Will bind the keystroke ctrl-1 to minimize every window of Windows Terminal and Google Chrome.

##### show
Show (restore) one or more applications: `show applications`

        applications = same as for hide.

    Example:

        bind 1:ctrl show 'Windows Terminal','Google Chrome'

##### toggle
Toggle one or more applications: `toggle applications`

        applications = same as for hide.

    An application is considered hidden when all its windows are minimized. Toggle restores hidden applications and
    minimizes the others.

    **Note:** If you specify current in this toggle operation it will not toggle properly because after the current application is hidden, it is no longer the current application anymore.

##### chain
Chain multiple operations to one binding: `chain opAndParams1 | opAndParams2 ...`

        opAndParamsX = any operation string (except sequence, hint and grid)

    Example:

        bind 1:ctrl chain push up | push right | push down | push left

    Will bind the keystroke ctrl-1 to push up on the first press, then push right on the second press, then push down on the third press, the push left on the fourth press and rotate back to pushing up on the fifth press (etc).

##### sequence
Activate a sequence of operations in one binding: `sequence opAndParams1 separator opAndParams 2 ...`

        opAndParamsX = any of the above operation strings (except chain and grid. hint must be last if present)
        separator = | or >. | will cause the next operation to be performed on the window focused at the time of
                    execution of that operation, > will cause the next operation to be performed on the window
                    focused at the start of the > chain.

    Example:

        bind 1:ctrl sequence focus right > push left | push right

    Will bind the keystroke ctrl-1 to first focus the window to the right, then push the previously focused window to the left, then push the newly focused window to the right. Obviously Hint will ignore `>` and `|` and just display because it doesn't care which window was focused.

##### layout
Activate a layout: `layout name`

        name = the name of the layout to activate (set using the layout directive)

    Example:

        bind 1:ctrl layout myLayout

    Will bind the keystroke ctrl-l to activate the layout called `myLayout`. Note that the layout **must** be created before you bind it.

##### focus
Focus a window in a direction or from an application: `focus direction|app`

        direction = right|left|up|above|down|below|behind
        app = an app name surrounded by quotes

    Example:

        bind 1:ctrl focus above

    Will bind the keystroke ctrl-1 to focus the window Slate finds to be above the currently focused window (from any application). Minimized windows are ignored. A couple global configuration options set using the `config` directive exist to tweak this. Also, up and above are the same. Down and below are also the same.

        bind 1:ctrl focus 'Google Chrome'

    Will bind the keystroke ctrl-1 to focus the main window of Google Chrome. The main window is its front-most window.

##### snapshot
Create a snapshot of your current window locations: `snapshot name options`

        name = the name of the snapshot to create (used in delete-snapshot and activate-snapshot)
        options = (optional) a semicolon separated list of any of the following options:
          save-to-disk -> saves the snapshot to disk so Slate will load it when it starts up next
          stack -> treats this snapshot as stack so you can use this binding multiple times to push snapshots on the stack

    Example:

         bind 1:ctrl snapshot theName save-to-disk;stack

    Will bind the keystroke ctrl-1 to create a snapshot called `theName`, save that snapshot to disk, and treat it as a stack so you can hit the keystroke multiple times to push snapshots onto the stack.

    **Note:** There is a menu option to take a snapshot of the current screen configuration. Snapshots are saved in `%APPDATA%\Slate\snapshots`.

##### delete-snapshot
Delete a snapshot: `delete-snapshot name options`

        name = the name of the snapshot to delete
        options = (optional) a semicolon separated list of any of the following options:
          all -> if the snapshot is a stack (if it isn't, this option is useless), this will delete all snapshots in the
                 stack (if this option is not specified, the default is to only delete the top snapshot of the stack).

    Example:

        bind 1:ctrl delete-snapshot theName all

##### activate-snapshot
Activate a snapshot: `activate-snapshot name options`

        name = the name of the snapshot to activate
        options = (optional) a semicolon separated list of any of the following options:
          delete -> this will delete the snapshot after activating it (if the snapshot is a stack, it will pop the top
                    snapshot off and keep the rest)

    Example:

        bind 1:ctrl activate-snapshot theName delete

##### hint
Show Window Hints (similar to Link Hints in Vimium except for Windows): `hint characters`

        characters = (optional) a simple string of characters to be used for the hints. each hint consists of one
                     character. if there are more windows than characters then some windows will not get hints.
                     this string can contain any of the single character Allowed Keys. Letters may be upper case or
                     lower case, but both will be bound to the lowercase letter for the hint. Using upper or lower
                     case only changes how they are displayed. The default string of characters is
                     "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"

    Example:

        bind 1:ctrl hint QWERTYUIOP

    Will bind the keystroke ctrl-1 to show Window Hints using the letters `Q`, `W`, `E`, `R`, `T`, `Y`, `U`, `I`, `O`, and `P`. This will show an overlay over every window on screen containing one of those letters. While the overlays are showing, if one of those letters is pressed, the corresponding window will be focused. If there are more than 10 windows, some windows will not get hints. Pressing ESC will dismiss the hints.

    **Note:** There are *tons* of config options to tweak this.

##### grid
Show a Grid to one-off resize and move windows: `grid options`

        options is a whitespace separated list of:
          padding:<integer> = the padding between cells
          screenRef:width,height = width and height are integers specifying the width and height of the grid
                                   (number of cells, not absolute size). screenRef is either the screenID or
                                   screen resolution (widthxheight)

    Example:

        bind 1:ctrl grid padding:5 1680x1050:16,9 1050x1680:9,16

    Will bind the keystroke ctrl-1 to show Grids on each screen. The default width and height are 12. This will set the padding between the cells to be 5. Also, this will change the width and height of the grid on the monitor with the resolution 1680x1050 to 16 and 9 respectively. For the monitor with the resolution 1050x1680, it will set the width to 9 and height to 16. Click and drag over cells of any grid to move the focused window onto them. Press ESC to close the grids.

    **Note:** There are a bunch of config options to tweak how this looks.

##### relaunch
Relaunch Slate: `relaunch`

    Example:

        bind 1:ctrl relaunch

    Will bind the keystroke ctrl-1 to relaunch Slate. This will also reload the `.slate` file from scratch.

##### undo
Undo an Operation: `undo`

    Example

        bind 1:ctrl undo

    Will bind the keystroke ctrl-1 to undo the last binding that was triggered. By default you can undo up to the last 10 commands. This can be changed using the `undoMaxStackSize` config. Also, you can only undo movement-based operations. Focus-related operations will not undo.

##### switch
A Better Application Switcher: `switch options`

        options = (optional) a whitespace separated list of keys used while the switcher is open (with the
                  binding's modifiers held):
          back:key       -> select the previous application (default `)
          quit:key       -> mark the selected application to be closed (default q)
          force-quit:key -> mark the selected application to be killed (default f)
          hide:key       -> minimize or restore the selected application (default h)

    Example:

        bind tab:alt switch

    Will replace the Windows application switcher (Alt+Tab). Keep Alt held and press Tab (or Shift+Tab, or the back
    key) to select an application; release Alt to switch to it and close or kill the marked applications.

     **Note:** There are *tons* of config options to tweak this.

### The `source` Directive

The source directive follows the following format (tokens may be separated by any number of spaces):

    source filename optional:if_exists

Where `filename` is the name of a file containing any of the directives above (including source). If no absolute path is specified, the user's home directory will be prepended to `filename`. If the user specifies the option `if_exists` as the second argument, Slate will not complain if it cannot find the file.

For Example:

    source ~/.slate.test if_exists

Will append all of the configurations from the file `%USERPROFILE%\.slate.test` to the current configuration if the file exists.

**Note:** You may use any aliases, layouts, etc that you specify before the source directive in the file you source. Any aliases, layouts, etc specified after cannot be used. Additionally, any aliases, layouts, etc that you specify in the file you source can be used after the source directive.

## Troubleshooting

Slate logs to `%LOCALAPPDATA%\Slate\slate.log`.

# Allowed Keys

Letters, digits and the punctuation keys `'` `,` `-` `.` `/` `;` `=` `` ` `` `[` `]` follow the active keyboard layout.
Named keys:

    backslash
    backspace (also: delete, the macOS name of this key)
    caps
    down
    end
    esc
    f1 ... f24
    forwarddelete
    help (the Insert key)
    home
    insert
    left
    mute
    pad*  pad+  pad-  pad.  pad/  pad=
    pad0 ... pad9
    padClear
    padEnter
    pageDown
    pageUp
    return
    right
    space
    tab
    up

# Development

    dotnet build
    dotnet test

* `src/Slate.Core`: the platform-independent domain: config parsing, expressions, screens, operations, layouts,
  snapshots, keyboard dispatch and the logic of hints, grid and switcher. Fully unit tested.
* `src/Slate.App`: the Windows application: Win32 windows and keyboard hook, overlays, dialogs and the tray icon.
* `tests/Slate.Core.Tests`: unit tests.
* `tests/Slate.App.IntegrationTests`: tests of the Win32 layer against the real OS.

Releases are published by GitHub Actions when a `vX.Y.Z` tag is pushed.

# License

GNU General Public License v3. See [LICENSE](LICENSE).
