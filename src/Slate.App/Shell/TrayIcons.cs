using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Slate.App.Shell;

/// <summary>Draws the tray icon: a slate of four tiles, highlighted while modal mode is active.</summary>
internal static partial class TrayIcons
{
    private const int Size = 32;
    private const int Margin = 3;
    private const int Gap = 2;
    private static readonly Color Idle = Color.FromArgb(240, 240, 240);
    private static readonly Color Active = Color.FromArgb(255, 170, 0);

    public static Icon Create(bool active)
    {
        using var bitmap = new Bitmap(Size, Size);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(active ? Active : Idle);
            int tile = (Size - 2 * Margin - Gap) / 2;
            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 2; column++)
                {
                    int x = Margin + column * (tile + Gap), y = Margin + row * (tile + Gap);
                    g.FillRectangle(brush, x, y, tile, tile);
                }
            }
        }
        nint handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint handle);
}
