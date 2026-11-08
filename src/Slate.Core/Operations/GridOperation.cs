using System.Globalization;
using Slate.Core.Expressions;
using Slate.Core.Interaction;
using Slate.Core.Keyboard;
using Slate.Core.Screens;
using Slate.Core.Text;
using Slate.Core.Windowing;
using static Slate.Core.Expressions.ExpressionVariables;

namespace Slate.Core.Operations;

/// <summary>
/// <c>grid [padding:N] [screen:columns,rows ...]</c>: shows a grid on every screen; dragging over cells moves the focused
/// window onto them. A screen is keyed by resolution (<c>1680x1050</c>) or screen id.
/// </summary>
public sealed class GridOperation(IReadOnlyDictionary<string, (int Columns, int Rows)> screenGrids, int padding) : Operation
{
    public const string PaddingKey = "padding";
    public const int DefaultCells = 12;
    public const int DefaultPadding = 2;

    public override OperationKind Kind => OperationKind.Grid;
    public IReadOnlyDictionary<string, (int Columns, int Rows)> ScreenGrids { get; } = screenGrids;
    public int Padding { get; } = padding;

    public static GridOperation Parse(string text)
    {
        var grids = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
        int padding = DefaultPadding;
        foreach (string token in StringTokenizer.Tokenize(text).Skip(1))
        {
            string[] split = token.Split(':');
            if (split.Length != 2) continue;
            if (split[0] == PaddingKey)
            {
                if (int.TryParse(split[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int p)) padding = p;
                continue;
            }
            string[] size = split[1].Split(',');
            if (size.Length == 2 &&
                int.TryParse(size[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int columns) &&
                int.TryParse(size[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rows) &&
                columns > 0 && rows > 0)
                grids[split[0]] = (columns, rows);
        }
        return new GridOperation(grids, padding);
    }

    public override bool Execute(OperationContext context) => ExecuteOn(context, context.Windows.FocusedWindow());

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        ScreenLayout screens = context.Screens();
        var specs = screens.Screens.Select(s => Spec(screens, s)).ToList();
        GridSession? session = null;
        IGridView view = context.Ui.ShowGrid(specs, context.Settings, selection => session?.Select(selection));
        session = new GridSession(context, view, window, specs);
        context.Keyboard.BeginCapture(session);
        return true;
    }

    public GridSpec Spec(ScreenLayout screens, ScreenInfo screen)
    {
        string resolution = ScreenLayout.ResolutionString(screen.Frame.Size);
        string referenceId = screens.ToReferenceId(screen.Id).ToString(CultureInfo.InvariantCulture);
        var (columns, rows) = ScreenGrids.TryGetValue(resolution, out var byResolution) ? byResolution
            : ScreenGrids.TryGetValue(referenceId, out var byId) ? byId
            : (DefaultCells, DefaultCells);
        return new GridSpec(screen.Id, screen.Frame, columns, rows, Padding, screen.Scale);
    }

    /// <summary>The move that puts a window on the selected cells of a <paramref name="columns"/> x <paramref name="rows"/> grid.</summary>
    public static MoveOperation MoveFor(GridSelection selection, int columns, int rows)
    {
        string F(int v) => v.ToString(CultureInfo.InvariantCulture);
        var topLeft = new ExpressionPoint(
            $"{ScreenOriginX}+({ScreenSizeX}*{F(selection.Column)}/{F(columns)})",
            $"{ScreenOriginY}+({ScreenSizeY}*{F(selection.Row)}/{F(rows)})");
        var size = new ExpressionPoint(
            $"{ScreenSizeX}*{F(selection.ColumnSpan)}/{F(columns)}",
            $"{ScreenSizeY}*{F(selection.RowSpan)}/{F(rows)}");
        return new MoveOperation(OperationKind.Grid, topLeft, size, screenReference: null, selection.ScreenId);
    }
}

/// <summary>Keyboard handling while grids are shown: Esc closes them; a selection moves the window and closes them.</summary>
public sealed class GridSession(OperationContext context, IGridView view, IWindow? window, IReadOnlyList<GridSpec> specs) : IKeyboardCapture
{
    private bool open = true;

    public (TimeSpan Before, TimeSpan Between) RepeatDelays => (Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    public CaptureResult OnKeyDown(Keystroke keystroke, bool isRepeat)
    {
        if (keystroke.Key.VirtualKey != VirtualKeys.Escape) return CaptureResult.PassThrough;
        Close();
        return CaptureResult.Handled;
    }

    public void OnModifiersChanged(Modifiers held) { }

    public void Cancel() => Close();

    /// <summary>Moves the window onto the selected cells of the grid it was made on.</summary>
    public void Select(GridSelection selection)
    {
        if (!open) return;
        Close();
        GridSpec? spec = specs.FirstOrDefault(s => s.ScreenId == selection.ScreenId);
        if (window is null || spec is null) return;
        window.Focus();
        GridOperation.MoveFor(selection, spec.Columns, spec.Rows).ExecuteOn(context, window);
    }

    private void Close()
    {
        if (!open) return;
        open = false;
        view.Close();
        context.Keyboard.EndCapture(this);
    }
}
