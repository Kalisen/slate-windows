using System.Text.Json;

namespace Slate.Core.Snapshots;

/// <summary>Persists snapshots marked <c>save-to-disk</c>.</summary>
public interface ISnapshotStore
{
    /// <summary>The stored JSON, or null when nothing was saved yet.</summary>
    string? Load();

    void Save(string json);
}

/// <summary>Reserved snapshot names.</summary>
public static class SnapshotNames
{
    /// <summary>Snapshot taken and restored from the tray menu.</summary>
    public const string Menu = "menuSnapshot";

    /// <summary>Stack of snapshots taken before each undoable operation.</summary>
    public const string Undo = "_internal_UndoSnapshot";
}

/// <summary>All named snapshots, saved through an <see cref="ISnapshotStore"/> after every change.</summary>
public sealed class SnapshotRepository(ISnapshotStore store)
{
    private readonly Dictionary<string, SnapshotList> lists = [];

    public IReadOnlyDictionary<string, SnapshotList> Lists => lists;

    public bool Has(string name) => lists.TryGetValue(name, out var list) && list.Snapshots.Count > 0;

    /// <summary>Loads saved snapshots. Throws <see cref="JsonException"/> when the stored data is corrupt.</summary>
    public void Load()
    {
        string? json = store.Load();
        if (string.IsNullOrWhiteSpace(json)) return;
        var saved = JsonSerializer.Deserialize<Dictionary<string, SnapshotList>>(json)
            ?? throw new JsonException("Snapshots file is not a JSON object");
        foreach (var list in saved.Values) lists[list.Name] = list;
    }

    public void Add(string name, Snapshot snapshot, bool saveToDisk, bool isStack, int maxStackSize)
    {
        if (!lists.TryGetValue(name, out var list)) lists[name] = list = new SnapshotList { Name = name };
        list.SaveToDisk = saveToDisk;
        list.IsStack = isStack;
        list.Add(snapshot, maxStackSize);
        Save();
    }

    /// <summary>Removes the top snapshot when <paramref name="popOnly"/> is set, otherwise the whole list.</summary>
    public void Delete(string name, bool popOnly)
    {
        if (popOnly) lists.GetValueOrDefault(name)?.Pop(remove: true);
        else lists.Remove(name);
        Save();
    }

    public Snapshot? Pop(string name, bool remove)
    {
        Snapshot? snapshot = lists.GetValueOrDefault(name)?.Pop(remove);
        Save();
        return snapshot;
    }

    private void Save()
    {
        var persisted = lists.Where(p => p.Value.SaveToDisk).ToDictionary(p => p.Key, p => p.Value);
        store.Save(JsonSerializer.Serialize(persisted));
    }
}
