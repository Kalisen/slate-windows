using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Slate.App.Interop;
using Slate.Core.Configuration;

namespace Slate.App.Overlays;

/// <summary>
/// A borderless, topmost, non-activating window drawn with per-pixel alpha (UpdateLayeredWindow), like the translucent
/// rounded panels of the macOS version. Coordinates are physical pixels.
/// </summary>
internal abstract class LayeredOverlay : Form
{
    protected LayeredOverlay(Rectangle bounds, bool clickThrough)
    {
        ClickThrough = clickThrough;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Bounds = bounds;
    }

    private bool ClickThrough { get; }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            long exStyle = NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_NOACTIVATE;
            if (ClickThrough) exStyle |= NativeMethods.WS_EX_TRANSPARENT;
            cp.ExStyle |= (int)exStyle;
            return cp;
        }
    }

    /// <summary>Draws the overlay; the graphics origin is the overlay's top-left corner.</summary>
    protected abstract void Draw(Graphics graphics);

    public void ShowOverlay()
    {
        Show();
        Render();
    }

    /// <summary>Redraws the overlay into the layered window.</summary>
    public void Render()
    {
        if (IsDisposed || Width <= 0 || Height <= 0) return;
        using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            Draw(g);
        }

        nint screenDc = NativeMethods.GetDC(0);
        nint memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
        nint hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        nint previous = NativeMethods.SelectObject(memoryDc, hBitmap);
        try
        {
            var size = new SIZE { Cx = Width, Cy = Height };
            var source = new POINT();
            var destination = new POINT { X = Left, Y = Top };
            var blend = new BLENDFUNCTION
            {
                BlendOp = NativeMethods.AC_SRC_OVER,
                SourceConstantAlpha = byte.MaxValue,
                AlphaFormat = NativeMethods.AC_SRC_ALPHA,
            };
            NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, NativeMethods.ULW_ALPHA);
        }
        finally
        {
            NativeMethods.SelectObject(memoryDc, previous);
            NativeMethods.DeleteObject(hBitmap);
            NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(0, screenDc);
        }
    }

    public static Color ToColor(RgbaColor c) => Color.FromArgb((int)Math.Round(c.Alpha * 255), c.R, c.G, c.B);

    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (d <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Font CreateFont(string name, float pixelSize)
    {
        try
        {
            return new Font(name, Math.Max(1, pixelSize), FontStyle.Regular, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)
        {
            return new Font(ConfigDefaults.OverlayFontName, Math.Max(1, pixelSize), FontStyle.Regular, GraphicsUnit.Pixel);
        }
    }
}
