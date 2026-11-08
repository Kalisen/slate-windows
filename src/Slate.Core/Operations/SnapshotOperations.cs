using Slate.Core.Configuration;
using Slate.Core.Snapshots;
using Slate.Core.Text;
using Slate.Core.Windowing;

namespace Slate.Core.Operations;

/// <summary><c>snapshot name [save-to-disk;stack]</c>: records the position of every titled window.</summary>
public sealed class SnapshotOperation(string name, bool saveToDisk, bool isStack, int? maxStackSize = null) : Operation
{
    public const string SaveToDiskOption = "save-to-disk";
    public const string StackOption = "stack";

    public override OperationKind Kind => OperationKind.Snapshot;
    public string Name { get; } = name;
    public bool SaveToDisk { get; } = saveToDisk;
    public bool IsStack { get; } = isStack;

    public static SnapshotOperation Parse(string text)
    {
        var (name, options) = ParseNameAndOptions(text, "snapshot name options");
        return new SnapshotOperation(name, options.Contains(SaveToDiskOption), options.Contains(StackOption));
    }

    public override bool Execute(OperationContext context) => ExecuteOn(context, null);

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        var snapshot = new Snapshot();
        foreach (IApplication app in context.Windows.Applications())
        {
            foreach (IWindow w in app.Windows.Where(w => w.Title.Length > 0))
            {
                var frame = w.Frame;
                snapshot.Add(new WindowSnapshot(app.Name, w.Title, frame.X, frame.Y, frame.Width, frame.Height));
            }
        }
        int stackSize = maxStackSize ?? context.Settings.GetInt(ConfigKeys.SnapshotMaxStackSize);
        context.Snapshots.Add(Name, snapshot, SaveToDisk, IsStack, stackSize);
        return true;
    }

    internal static (string Name, string[] Options) ParseNameAndOptions(string text, string format)
    {
        var tokens = StringTokenizer.Tokenize(text, 3);
        if (tokens.Count < 2)
            throw new ConfigException("Invalid Parameters", $"Invalid Parameters in '{text}'. Snapshot operations require the following format: '{format}'");
        return (tokens[1], tokens.Count > 2 ? tokens[2].Split(';') : []);
    }
}

/// <summary><c>activate-snapshot name [delete]</c>: moves windows back to their saved positions.</summary>
public sealed class ActivateSnapshotOperation(string name, bool delete, OperationKind kind = OperationKind.ActivateSnapshot) : Operation
{
    public const string DeleteOption = "delete";

    public override OperationKind Kind { get; } = kind;
    public string Name { get; } = name;
    public bool Delete { get; } = delete;

    public static ActivateSnapshotOperation Parse(string text)
    {
        var (name, options) = SnapshotOperation.ParseNameAndOptions(text, "activate-snapshot name options");
        return new ActivateSnapshotOperation(name, options.Contains(DeleteOption));
    }

    public override bool Execute(OperationContext context) => ExecuteOn(context, null);

    public override bool ExecuteOn(OperationContext context, IWindow? window) => Activate(context, Name, Delete);

    /// <summary>Pairs each titled window with the saved window of its application whose title matches best.</summary>
    public static bool Activate(OperationContext context, string name, bool delete)
    {
        Snapshot? snapshot = context.Snapshots.Pop(name, delete);
        if (snapshot is null) return true;
        foreach (IApplication app in context.Windows.Applications())
        {
            if (!snapshot.Apps.TryGetValue(app.Name, out var saved) || saved.Count == 0) continue;
            var match = context.Settings.GetForApp(ConfigKeys.SnapshotTitleMatch, app.Name);
            var titleMatch = SlateSettings.ParseEnum<SnapshotTitleMatch>(match, ConfigDefaults.Values[ConfigKeys.SnapshotTitleMatch]);
            foreach (IWindow w in app.Windows.Where(w => w.Title.Length > 0))
            {
                WindowSnapshot best = BestMatch(w.Title, saved, titleMatch);
                w.Move(best.Frame.TopLeft);
                w.Resize(best.Frame.Size);
            }
        }
        return true;
    }

    public static WindowSnapshot BestMatch(string title, IReadOnlyList<WindowSnapshot> saved, SnapshotTitleMatch match) =>
        match == SnapshotTitleMatch.Sequential
            ? saved.MaxBy(s => StringDistance.Sequential(title, s.Title))!
            : saved.MinBy(s => StringDistance.Levenshtein(title, s.Title))!;
}

/// <summary><c>delete-snapshot name [all]</c>: pops the top of a snapshot stack, or deletes it all.</summary>
public sealed class DeleteSnapshotOperation(string name, bool all) : Operation
{
    public const string AllOption = "all";

    public override OperationKind Kind => OperationKind.DeleteSnapshot;
    public string Name { get; } = name;
    public bool All { get; } = all;

    public static DeleteSnapshotOperation Parse(string text)
    {
        var (name, options) = SnapshotOperation.ParseNameAndOptions(text, "delete-snapshot name options");
        return new DeleteSnapshotOperation(name, options.Contains(AllOption));
    }

    public override bool Execute(OperationContext context) => ExecuteOn(context, null);

    public override bool ExecuteOn(OperationContext context, IWindow? window)
    {
        context.Snapshots.Delete(Name, popOnly: !All);
        return true;
    }
}
