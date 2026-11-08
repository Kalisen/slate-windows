using Slate.Core.Configuration;
using Slate.Core.Text;
using Slate.Core.Windowing;

namespace Slate.Core.Operations;

/// <summary>Separators between operations in <c>chain</c>, <c>sequence</c> and <c>layout</c>.</summary>
public static class OperationSeparators
{
    /// <summary>Separates operations; in a sequence, the next one targets the window focused at that time.</summary>
    public const string Pipe = " | ";

    /// <summary>In a sequence, the next operation targets the window focused at the start of the <c>&gt;</c> group.</summary>
    public const string GreaterThan = " > ";
}

/// <summary><c>chain op | op ...</c>: each press runs the next operation for the window it last moved.</summary>
public sealed class ChainOperation(IReadOnlyList<Operation> operations) : Operation
{
    private readonly Dictionary<WindowState, int> nextIndex = [];

    public override OperationKind Kind => OperationKind.Chain;
    public IReadOnlyList<Operation> Operations { get; } = operations;

    public static ChainOperation Parse(string text, ParseContext context)
    {
        var tokens = StringTokenizer.Tokenize(text, 2);
        if (tokens.Count < 2)
            throw new ConfigException("Invalid Parameters", $"Invalid Parameters in '{text}'. Chain operations require the following format: 'chain op[|op]+'");
        var operations = tokens[1].Split(OperationSeparators.Pipe).Select(op => OperationParser.Parse(op, context)).ToList();
        return new ChainOperation(operations);
    }

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        if (window is null) return false;
        int index = nextIndex.GetValueOrDefault(WindowState.Of(window));
        bool success = Operations[index].ExecuteOn(context, window);
        if (success) nextIndex[WindowState.Of(window)] = index + 1 < Operations.Count ? index + 1 : 0;
        return success;
    }
}

/// <summary>
/// <c>sequence op | op &gt; op</c>: runs all operations. Each <c>|</c> group starts by re-reading the focused window, and every
/// operation of a group (joined with <c>&gt;</c>) targets that window.
/// </summary>
public sealed class SequenceOperation(IReadOnlyList<IReadOnlyList<Operation>> groups) : Operation
{
    public override OperationKind Kind => OperationKind.Sequence;
    public IReadOnlyList<IReadOnlyList<Operation>> Groups { get; } = groups;

    public static SequenceOperation Parse(string text, ParseContext context)
    {
        var tokens = StringTokenizer.Tokenize(text, 2);
        if (tokens.Count < 2)
            throw new ConfigException("Invalid Parameters", $"Invalid Parameters in '{text}'. Sequence operations require the following format: 'sequence op [(\\||>) op]+'");
        var groups = tokens[1].Split(OperationSeparators.Pipe)
            .Select(group => (IReadOnlyList<Operation>)group.Split(OperationSeparators.GreaterThan).Select(op => OperationParser.Parse(op, context)).ToList())
            .ToList();
        return new SequenceOperation(groups);
    }

    public override bool Execute(OperationContext context) => ExecuteOn(context, null);

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        foreach (var group in Groups)
        {
            IWindow? target = context.Windows.FocusedWindow();
            foreach (Operation operation in group) operation.ExecuteOn(context, target);
        }
        return true;
    }
}
