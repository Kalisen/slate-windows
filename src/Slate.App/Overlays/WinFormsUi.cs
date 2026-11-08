using Slate.Core.Configuration;
using Slate.Core.Interaction;
using Slate.Core.Screens;

namespace Slate.App.Overlays;

/// <summary>Creates the hint, grid and switcher overlays.</summary>
internal sealed class WinFormsUi(IScreenProvider screens) : IInteractiveUi
{
    public IHintView ShowHints(IReadOnlyList<HintPlacement> hints, SlateSettings settings) => new HintView(hints, settings);

    public IGridView ShowGrid(IReadOnlyList<GridSpec> grids, SlateSettings settings, Action<GridSelection> onSelected) =>
        new GridView(grids, settings, onSelected);

    public ISwitcherView ShowSwitcher(IReadOnlyList<SwitcherItem> items, int selected, SlateSettings settings) =>
        new SwitcherView(screens.Screens(), items, selected, settings);
}
