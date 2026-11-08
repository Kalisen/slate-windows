using System.Globalization;
using System.Text;
using Slate.Core.Operations;
using Slate.Core.Screens;
using Slate.Core.Windowing;

namespace Slate.Core.Interaction;

/// <summary>The "Current Window Info" text: screen ids and the position of every window, in config terms.</summary>
public static class WindowInfoReport
{
    public static string Build(OperationContext context)
    {
        ScreenLayout screens = context.Screens();
        var text = new StringBuilder();
        var c = CultureInfo.InvariantCulture;
        text.AppendLine("----------------- Screens -----------------");
        var resolutions = screens.ResolutionStrings();
        for (int i = 0; i < screens.Count; i++)
        {
            text.AppendLine(c, $"Left To Right ID: {screens.LeftToRightIndexOf(i)}");
            text.AppendLine(c, $"  System ID: {i}");
            text.AppendLine(c, $"  Resolution: {resolutions[i]}");
        }
        text.AppendLine();
        text.AppendLine("----------------- Windows -----------------");
        foreach (IApplication app in context.Windows.Applications())
        {
            text.AppendLine();
            text.AppendLine(c, $"Application: {app.Name}");
            foreach (IWindow window in app.Windows.Where(w => w.Title.Length > 0))
            {
                var frame = window.Frame;
                int screenId = screens.ScreenIdForRect(frame);
                var visible = screens.VisibleFrame(screenId);
                text.AppendLine(c, $"  Window: '{window.Title}'");
                text.AppendLine(c, $"    Screen ID (Left to Right): {screens.LeftToRightIndexOf(screenId)}");
                text.AppendLine(c, $"    Size: ({(int)frame.Width}, {(int)frame.Height})");
                text.AppendLine(c, $"    Top Left: (screenOriginX+{(int)(frame.X - visible.X)}, screenOriginY+{(int)(frame.Y - visible.Y)})");
            }
        }
        return text.ToString();
    }
}
