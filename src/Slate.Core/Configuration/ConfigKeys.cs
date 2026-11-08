namespace Slate.Core.Configuration;

/// <summary>Names accepted by the <c>config</c> directive.</summary>
public static class ConfigKeys
{
    public const string DefaultToCurrentScreen = "defaultToCurrentScreen";
    public const string NudgePercentOf = "nudgePercentOf";
    public const string ResizePercentOf = "resizePercentOf";
    public const string RepeatOnHoldOps = "repeatOnHoldOps";
    public const string SecondsBeforeRepeat = "secondsBeforeRepeat";
    public const string SecondsBetweenRepeat = "secondsBetweenRepeat";
    public const string CheckDefaultsOnLoad = "checkDefaultsOnLoad";
    public const string FocusCheckWidth = "focusCheckWidth";
    public const string FocusCheckWidthMax = "focusCheckWidthMax";
    public const string FocusPreferSameApp = "focusPreferSameApp";
    public const string OrderScreensLeftToRight = "orderScreensLeftToRight";
    public const string WindowHintsFontName = "windowHintsFontName";
    public const string WindowHintsFontSize = "windowHintsFontSize";
    public const string WindowHintsFontColor = "windowHintsFontColor";
    public const string WindowHintsWidth = "windowHintsWidth";
    public const string WindowHintsHeight = "windowHintsHeight";
    public const string WindowHintsBackgroundColor = "windowHintsBackgroundColor";
    public const string WindowHintsDuration = "windowHintsDuration";
    public const string WindowHintsRoundedCornerSize = "windowHintsRoundedCornerSize";
    public const string WindowHintsIgnoreHiddenWindows = "windowHintsIgnoreHiddenWindows";
    public const string WindowHintsTopLeftX = "windowHintsTopLeftX";
    public const string WindowHintsTopLeftY = "windowHintsTopLeftY";
    public const string WindowHintsOrder = "windowHintsOrder";
    public const string WindowHintsShowIcons = "windowHintsShowIcons";
    public const string WindowHintsIconAlpha = "windowHintsIconAlpha";
    public const string WindowHintsSpread = "windowHintsSpread";
    public const string WindowHintsSpreadSearchWidth = "windowHintsSpreadSearchWidth";
    public const string WindowHintsSpreadSearchHeight = "windowHintsSpreadSearchHeight";
    public const string WindowHintsSpreadPadding = "windowHintsSpreadPadding";
    public const string SwitchIconSize = "switchIconSize";
    public const string SwitchIconPadding = "switchIconPadding";
    public const string SwitchBackgroundColor = "switchBackgroundColor";
    public const string SwitchSelectedBackgroundColor = "switchSelectedBackgroundColor";
    public const string SwitchSelectedBorderColor = "switchSelectedBorderColor";
    public const string SwitchSelectedBorderSize = "switchSelectedBorderSize";
    public const string SwitchRoundedCornerSize = "switchRoundedCornerSize";
    public const string SwitchOrientation = "switchOrientation";
    public const string SwitchSecondsBeforeRepeat = "switchSecondsBeforeRepeat";
    public const string SwitchSecondsBetweenRepeat = "switchSecondsBetweenRepeat";
    public const string SwitchStopRepeatAtEdge = "switchStopRepeatAtEdge";
    public const string SwitchOnlyFocusMainWindow = "switchOnlyFocusMainWindow";
    public const string SwitchFontSize = "switchFontSize";
    public const string SwitchFontColor = "switchFontColor";
    public const string SwitchFontName = "switchFontName";
    public const string SwitchShowTitles = "switchShowTitles";
    public const string SwitchType = "switchType";
    public const string SwitchSelectedPadding = "switchSelectedPadding";
    public const string KeyboardLayout = "keyboardLayout";
    public const string SnapshotTitleMatch = "snapshotTitleMatch";
    public const string SnapshotMaxStackSize = "snapshotMaxStackSize";
    public const string UndoMaxStackSize = "undoMaxStackSize";
    public const string UndoOps = "undoOps";
    public const string GridBackgroundColor = "gridBackgroundColor";
    public const string GridRoundedCornerSize = "gridRoundedCornerSize";
    public const string GridCellBackgroundColor = "gridCellBackgroundColor";
    public const string GridCellSelectedColor = "gridCellSelectedColor";
    public const string GridCellRoundedCornerSize = "gridCellRoundedCornerSize";
    public const string LayoutFocusOnActivate = "layoutFocusOnActivate";
    public const string ModalEscapeKey = "modalEscapeKey";
}

/// <summary>Default value of every config, as written in a <c>.slate</c> file.</summary>
public static class ConfigDefaults
{
    /// <summary>Default font of hint and switcher overlays (Helvetica on macOS).</summary>
    public const string OverlayFontName = "Segoe UI";

    public static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
    {
        [ConfigKeys.DefaultToCurrentScreen] = "false",
        [ConfigKeys.NudgePercentOf] = "screenSize",
        [ConfigKeys.ResizePercentOf] = "screenSize",
        [ConfigKeys.RepeatOnHoldOps] = "resize,nudge",
        [ConfigKeys.SecondsBeforeRepeat] = "0.4",
        [ConfigKeys.SecondsBetweenRepeat] = "0.2",
        [ConfigKeys.CheckDefaultsOnLoad] = "false",
        [ConfigKeys.FocusCheckWidth] = "100",
        [ConfigKeys.FocusCheckWidthMax] = "100",
        [ConfigKeys.FocusPreferSameApp] = "true",
        [ConfigKeys.OrderScreensLeftToRight] = "true",
        [ConfigKeys.WindowHintsFontName] = OverlayFontName,
        [ConfigKeys.WindowHintsFontSize] = "40",
        [ConfigKeys.WindowHintsFontColor] = "255;255;255;1.0",
        [ConfigKeys.WindowHintsWidth] = "100",
        [ConfigKeys.WindowHintsHeight] = "100",
        [ConfigKeys.WindowHintsBackgroundColor] = "50;53;58;0.9",
        [ConfigKeys.WindowHintsDuration] = "3",
        [ConfigKeys.WindowHintsRoundedCornerSize] = "5",
        [ConfigKeys.WindowHintsIgnoreHiddenWindows] = "true",
        [ConfigKeys.WindowHintsTopLeftX] = "(windowSizeX/2)-(windowHintsWidth/2);0",
        [ConfigKeys.WindowHintsTopLeftY] = "(windowSizeY/2)-(windowHintsHeight/2);0",
        [ConfigKeys.WindowHintsOrder] = "leftToRight",
        [ConfigKeys.WindowHintsShowIcons] = "false",
        [ConfigKeys.WindowHintsIconAlpha] = "1.0",
        [ConfigKeys.WindowHintsSpread] = "false",
        [ConfigKeys.WindowHintsSpreadSearchWidth] = "40",
        [ConfigKeys.WindowHintsSpreadSearchHeight] = "40",
        [ConfigKeys.WindowHintsSpreadPadding] = "20",
        [ConfigKeys.SwitchIconSize] = "100",
        [ConfigKeys.SwitchIconPadding] = "5",
        [ConfigKeys.SwitchBackgroundColor] = "50;53;58;0.3",
        [ConfigKeys.SwitchSelectedBackgroundColor] = "50;53;58;0.9",
        [ConfigKeys.SwitchSelectedBorderColor] = "230;230;230;0.9",
        [ConfigKeys.SwitchSelectedBorderSize] = "2",
        [ConfigKeys.SwitchRoundedCornerSize] = "5",
        [ConfigKeys.SwitchOrientation] = "horizontal",
        [ConfigKeys.SwitchSecondsBeforeRepeat] = "0.3",
        [ConfigKeys.SwitchSecondsBetweenRepeat] = "0.03",
        [ConfigKeys.SwitchStopRepeatAtEdge] = "true",
        [ConfigKeys.SwitchOnlyFocusMainWindow] = "true",
        [ConfigKeys.SwitchFontSize] = "14",
        [ConfigKeys.SwitchFontColor] = "255;255;255;1.0",
        [ConfigKeys.SwitchFontName] = OverlayFontName,
        [ConfigKeys.SwitchShowTitles] = "false",
        [ConfigKeys.SwitchType] = "app",
        [ConfigKeys.SwitchSelectedPadding] = "10",
        [ConfigKeys.KeyboardLayout] = "qwerty",
        [ConfigKeys.SnapshotTitleMatch] = "levenshtein",
        [ConfigKeys.SnapshotMaxStackSize] = "0",
        [ConfigKeys.UndoMaxStackSize] = "10",
        [ConfigKeys.UndoOps] = "activate-snapshot,chain,grid,layout,move,push,nudge,corner,throw,resize,sequence,shell",
        [ConfigKeys.GridBackgroundColor] = "75;77;81;1.0",
        [ConfigKeys.GridRoundedCornerSize] = "5",
        [ConfigKeys.GridCellBackgroundColor] = "100;106;116;0.8",
        [ConfigKeys.GridCellSelectedColor] = "50;53;58;0.8",
        [ConfigKeys.GridCellRoundedCornerSize] = "5",
        [ConfigKeys.LayoutFocusOnActivate] = "false",
        [ConfigKeys.ModalEscapeKey] = "",
    };
}
