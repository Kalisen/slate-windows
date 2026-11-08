using Slate.Core.Configuration;
using Slate.Core.Operations;
using Slate.Core.Text;

namespace Slate.Core.Keyboard;

/// <summary>A <c>bind keystroke operation</c> directive.</summary>
public sealed class Binding(BindingKeystroke keystroke, Operation operation, bool repeatOnHold, string text)
{
    public BindingKeystroke Keystroke { get; } = keystroke;
    public Operation Operation { get; } = operation;

    /// <summary>Whether holding the keys repeats the operation (operations listed in <c>repeatOnHoldOps</c>).</summary>
    public bool RepeatOnHold { get; } = repeatOnHold;

    public string Text { get; } = text;

    public bool IsModal => Keystroke.ModalKeystroke is not null;

    public static Binding Parse(string line, ParseContext context)
    {
        var tokens = StringTokenizer.Tokenize(line, 3);
        if (tokens.Count <= 2) throw new ConfigException("Unrecognized Bind", line);
        var keystroke = BindingKeystroke.Parse(tokens[1], context.KeyboardLayout);
        string operationText = tokens[2];
        Operation operation = OperationParser.Parse(operationText, context);
        if (operation is SwitchOperation switchOperation) switchOperation.Trigger = keystroke.Keystroke;
        string name = StringTokenizer.FirstToken(operationText);
        bool repeat = context.Settings.GetCommaList(ConfigKeys.RepeatOnHoldOps).Contains(name);
        return new Binding(keystroke, operation, repeat, line);
    }
}

/// <summary>All bindings of a configuration, split into global and modal ones.</summary>
public sealed class KeyBindings
{
    private readonly Dictionary<Keystroke, List<Binding>> modal = [];

    public List<Binding> Global { get; } = [];

    /// <summary>Modal bindings grouped by the keystroke that enters their modal mode.</summary>
    public IReadOnlyDictionary<Keystroke, List<Binding>> Modal => modal;

    public void Add(Binding binding)
    {
        if (binding.Keystroke.ModalKeystroke is { } modalKey)
        {
            if (!modal.TryGetValue(modalKey, out var list)) modal[modalKey] = list = [];
            list.Add(binding);
        }
        else
        {
            Global.Add(binding);
        }
    }

    public IEnumerable<Binding> All => Global.Concat(modal.Values.SelectMany(b => b));
}
