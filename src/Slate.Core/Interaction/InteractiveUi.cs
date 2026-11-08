using Slate.Core.Configuration;
using Slate.Core.Geometry;
using Slate.Core.Windowing;

namespace Slate.Core.Interaction;

/// <summary>A hint to draw: <see cref="Label"/> shown in <see cref="Bounds"/> for <see cref="Window"/>.</summary>
public sealed record HintPlacement(IWindow Window, char Label, Rect Bounds, double Scale = 1.0);

public interface IHintView
{
    void Close();
}

/// <summary>A grid shown centered on a screen: <see cref="Columns"/> x <see cref="Rows"/> cells.</summary>
public sealed record GridSpec(int ScreenId, Rect ScreenFrame, int Columns, int Rows, double Padding, double Scale = 1.0);

/// <summary>Cells selected in a grid, as cell indices with the origin at the top-left cell.</summary>
public sealed record GridSelection(int ScreenId, int Column, int Row, int ColumnSpan, int RowSpan);

public interface IGridView
{
    void Close();
}

/// <summary>Visual state of one application in the switcher.</summary>
public sealed record SwitcherItem(IApplication Application, bool Hidden, bool Quitting, bool ForceQuitting);

public interface ISwitcherView
{
    void Update(IReadOnlyList<SwitcherItem> items, int selected);

    void Close();
}

/// <summary>Overlays drawn by the platform layer.</summary>
public interface IInteractiveUi
{
    IHintView ShowHints(IReadOnlyList<HintPlacement> hints, SlateSettings settings);

    /// <summary>Shows one grid per spec; <paramref name="onSelected"/> runs when the user drags over cells.</summary>
    IGridView ShowGrid(IReadOnlyList<GridSpec> grids, SlateSettings settings, Action<GridSelection> onSelected);

    ISwitcherView ShowSwitcher(IReadOnlyList<SwitcherItem> items, int selected, SlateSettings settings);
}
