using System.Drawing;

namespace Slate.App.Shell;

/// <summary>Shows screen ids and window positions to help write configs.</summary>
internal sealed class WindowInfoForm : Form
{
    private const string MonospaceFont = "Cascadia Mono";
    private const float FontSize = 9.5f;
    private readonly TextBox text;
    private readonly Func<string> report;

    public WindowInfoForm(Func<string> report)
    {
        this.report = report;
        Text = "Slate - Current Window Info";
        StartPosition = FormStartPosition.Manual;
        Size = new Size(720, 600);
        MinimumSize = new Size(360, 240);
        ShowIcon = false;

        text = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font(MonospaceFont, FontSize),
        };
        var refresh = new Button { Text = "Refresh", Dock = DockStyle.Bottom, Height = 32 };
        refresh.Click += (_, _) => Reload();
        Controls.Add(text);
        Controls.Add(refresh);
    }

    /// <summary>Shows the window centered on the screen under the mouse.</summary>
    public void ShowCentered()
    {
        Reload();
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2);
        Show();
        Activate();
    }

    private void Reload() => text.Text = report().ReplaceLineEndings(Environment.NewLine);
}
