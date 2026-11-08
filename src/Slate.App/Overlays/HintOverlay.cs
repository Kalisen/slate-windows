using System.Drawing;
using System.Drawing.Imaging;
using Slate.App.Windowing;
using Slate.Core.Configuration;
using Slate.Core.Interaction;

namespace Slate.App.Overlays;

/// <summary>One window hint: a rounded panel with the hint character and optionally the application icon.</summary>
internal sealed class HintOverlay : LayeredOverlay
{
    private readonly HintPlacement hint;
    private readonly SlateSettings settings;

    public HintOverlay(HintPlacement hint, SlateSettings settings)
        : base(Rectangle.Round(new RectangleF((float)hint.Bounds.X, (float)hint.Bounds.Y, (float)hint.Bounds.Width, (float)hint.Bounds.Height)), clickThrough: true)
    {
        this.hint = hint;
        this.settings = settings;
    }

    protected override void Draw(Graphics g)
    {
        var bounds = new RectangleF(0, 0, Width, Height);
        float corner = (float)(settings.GetDouble(ConfigKeys.WindowHintsRoundedCornerSize) * hint.Scale);
        using (var path = RoundedRectangle(bounds, corner))
        using (var background = new SolidBrush(ToColor(settings.GetColor(ConfigKeys.WindowHintsBackgroundColor))))
            g.FillPath(background, path);

        if (settings.GetBool(ConfigKeys.WindowHintsShowIcons) && hint.Window.Application is Win32Application app &&
            ProcessInfoCache.IconFor(app.Details) is { } icon)
        {
            float alpha = (float)Math.Clamp(settings.GetDouble(ConfigKeys.WindowHintsIconAlpha), 0, 1);
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = alpha });
            g.DrawImage(icon, Rectangle.Round(bounds), 0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, attributes);
        }

        float fontSize = (float)(settings.GetDouble(ConfigKeys.WindowHintsFontSize) * hint.Scale);
        using Font font = CreateFont(settings.Get(ConfigKeys.WindowHintsFontName), fontSize);
        using var text = new SolidBrush(ToColor(settings.GetColor(ConfigKeys.WindowHintsFontColor)));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(hint.Label.ToString(), font, text, bounds, format);
    }
}

/// <summary>All hints shown by one <c>hint</c> operation.</summary>
internal sealed class HintView : IHintView
{
    private readonly List<HintOverlay> overlays;

    public HintView(IReadOnlyList<HintPlacement> hints, SlateSettings settings)
    {
        overlays = hints.Select(h => new HintOverlay(h, settings)).ToList();
        foreach (var overlay in overlays) overlay.ShowOverlay();
    }

    public void Close()
    {
        foreach (var overlay in overlays) overlay.Close();
        overlays.Clear();
    }
}
