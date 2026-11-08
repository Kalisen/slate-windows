using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Slate.App.Windowing;
using Slate.Core.Configuration;
using Slate.Core.Interaction;
using Slate.Core.Screens;

namespace Slate.App.Overlays;

/// <summary>Measurements of the switcher panel on one screen, in physical pixels.</summary>
internal sealed record SwitcherMetrics(float IconSize, float IconPadding, float SelectedPadding, float TitleHeight, float TitleWidth, bool Vertical, bool ShowTitles)
{
    public float Cell => IconSize + 2 * IconPadding;

    public Size PanelSize(int count) => Vertical
        ? new Size((int)(Cell + TitleWidth + 2 * SelectedPadding), (int)(count * Cell + 2 * SelectedPadding))
        : new Size((int)(count * Cell + 2 * SelectedPadding), (int)(Cell + TitleHeight + 2 * SelectedPadding));

    public RectangleF ItemBounds(int index) => Vertical
        ? new RectangleF(SelectedPadding, SelectedPadding + index * Cell, Cell + TitleWidth, Cell)
        : new RectangleF(SelectedPadding + index * Cell, SelectedPadding, Cell, Cell + TitleHeight);
}

/// <summary>The application switcher panel shown centered on one screen.</summary>
internal sealed class SwitcherOverlay : LayeredOverlay
{
    /// <summary>Opacity of the icon of a minimized (hidden) application.</summary>
    private const float HiddenIconAlpha = 0.2f;
    /// <summary>Width of the red stripes marking an application to quit.</summary>
    private const float QuitStripeWidth = 5;

    private readonly SlateSettings settings;
    private readonly SwitcherMetrics metrics;
    private readonly float scale;
    private IReadOnlyList<SwitcherItem> items;
    private int selected;

    public SwitcherOverlay(ScreenInfo screen, SwitcherMetrics metrics, IReadOnlyList<SwitcherItem> items, int selected, SlateSettings settings)
        : base(Centered(screen, metrics.PanelSize(items.Count)), clickThrough: true)
    {
        this.settings = settings;
        this.metrics = metrics;
        this.items = items;
        this.selected = selected;
        scale = (float)screen.Scale;
    }

    private static Rectangle Centered(ScreenInfo screen, Size size) => new(
        (int)(screen.Frame.X + (screen.Frame.Width - size.Width) / 2),
        (int)(screen.Frame.Y + (screen.Frame.Height - size.Height) / 2),
        size.Width, size.Height);

    public void Update(IReadOnlyList<SwitcherItem> newItems, int newSelected)
    {
        items = newItems;
        selected = newSelected;
        Render();
    }

    protected override void Draw(Graphics g)
    {
        float corner = (float)settings.GetDouble(ConfigKeys.SwitchRoundedCornerSize) * scale;
        using (var path = RoundedRectangle(new RectangleF(0, 0, Width, Height), corner))
        using (var background = new SolidBrush(ToColor(settings.GetColor(ConfigKeys.SwitchBackgroundColor))))
            g.FillPath(background, path);

        using Font font = CreateFont(settings.Get(ConfigKeys.SwitchFontName), (float)settings.GetDouble(ConfigKeys.SwitchFontSize) * scale);
        for (int i = 0; i < items.Count; i++) DrawItem(g, items[i], metrics.ItemBounds(i), i == selected, corner, font);
    }

    private void DrawItem(Graphics g, SwitcherItem item, RectangleF bounds, bool isSelected, float corner, Font font)
    {
        if (isSelected)
        {
            float border = (float)settings.GetDouble(ConfigKeys.SwitchSelectedBorderSize) * scale;
            var inner = RectangleF.Inflate(bounds, -border / 2, -border / 2);
            using var path = RoundedRectangle(inner, corner);
            using var background = new SolidBrush(ToColor(settings.GetColor(ConfigKeys.SwitchSelectedBackgroundColor)));
            g.FillPath(background, path);
            if (border > 0)
            {
                using var pen = new Pen(ToColor(settings.GetColor(ConfigKeys.SwitchSelectedBorderColor)), border);
                g.DrawPath(pen, path);
            }
        }

        var icon = new RectangleF(bounds.X + metrics.IconPadding, bounds.Y + metrics.IconPadding, metrics.IconSize, metrics.IconSize);
        if (item.Application is Win32Application app && ProcessInfoCache.IconFor(app.Details) is { } image)
        {
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = item.Hidden ? HiddenIconAlpha : 1f });
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, Rectangle.Round(icon), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }

        if (metrics.ShowTitles)
        {
            RectangleF title = metrics.Vertical
                ? new RectangleF(icon.Right + metrics.IconPadding, bounds.Y, bounds.Right - icon.Right - metrics.IconPadding, bounds.Height)
                : new RectangleF(bounds.X, icon.Bottom + metrics.IconPadding, bounds.Width, metrics.TitleHeight);
            using var brush = new SolidBrush(ToColor(settings.GetColor(ConfigKeys.SwitchFontColor)));
            using var format = new StringFormat
            {
                Alignment = metrics.Vertical ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
            };
            g.DrawString(item.Application.Name, font, brush, title, format);
        }

        if (item.Quitting || item.ForceQuitting) DrawQuitMark(g, bounds, item.ForceQuitting);
    }

    /// <summary>A red diagonal stripe for quit; a red cross for force quit.</summary>
    private void DrawQuitMark(Graphics g, RectangleF b, bool force)
    {
        using var pen = new Pen(Color.Red, QuitStripeWidth * scale);
        g.DrawLine(pen, b.Left, b.Bottom, b.Right, b.Top);
        if (force) g.DrawLine(pen, b.Left, b.Top, b.Right, b.Bottom);
    }
}

internal sealed class SwitcherView : ISwitcherView
{
    private readonly List<SwitcherOverlay> overlays = [];

    public SwitcherView(IReadOnlyList<ScreenInfo> screens, IReadOnlyList<SwitcherItem> items, int selected, SlateSettings settings)
    {
        foreach (ScreenInfo screen in screens)
        {
            var overlay = new SwitcherOverlay(screen, Metrics(screen, items, settings), items, selected, settings);
            overlays.Add(overlay);
            overlay.ShowOverlay();
        }
    }

    public void Update(IReadOnlyList<SwitcherItem> items, int selected)
    {
        foreach (var overlay in overlays) overlay.Update(items, selected);
    }

    public void Close()
    {
        foreach (var overlay in overlays) overlay.Close();
        overlays.Clear();
    }

    private static SwitcherMetrics Metrics(ScreenInfo screen, IReadOnlyList<SwitcherItem> items, SlateSettings settings)
    {
        float scale = (float)screen.Scale;
        float iconSize = (float)settings.GetDouble(ConfigKeys.SwitchIconSize) * scale;
        float iconPadding = (float)settings.GetDouble(ConfigKeys.SwitchIconPadding) * scale;
        float selectedPadding = (float)settings.GetDouble(ConfigKeys.SwitchSelectedPadding) * scale;
        bool vertical = settings.GetEnum<SwitchOrientation>(ConfigKeys.SwitchOrientation) == SwitchOrientation.Vertical;
        bool showTitles = settings.GetBool(ConfigKeys.SwitchShowTitles);
        float titleHeight = 0, titleWidth = 0;
        if (showTitles)
        {
            using Font font = LayeredOverlay.CreateFont(settings.Get(ConfigKeys.SwitchFontName), (float)settings.GetDouble(ConfigKeys.SwitchFontSize) * scale);
            titleHeight = font.GetHeight() + iconPadding;
            if (vertical)
            {
                titleWidth = items.Select(i => (float)TextRenderer.MeasureText(i.Application.Name, font).Width).DefaultIfEmpty(0).Max();
            }
        }
        return new SwitcherMetrics(iconSize, iconPadding, selectedPadding, titleHeight, titleWidth, vertical, showTitles);
    }
}
