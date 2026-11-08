using System.Text.Json.Serialization;
using Slate.Core.Geometry;

namespace Slate.Core.Snapshots;

/// <summary>The saved position of one window. JSON names match the macOS snapshots file.</summary>
public sealed record WindowSnapshot(
    [property: JsonPropertyName("app-name")] string AppName,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("width")] double Width,
    [property: JsonPropertyName("height")] double Height)
{
    [JsonIgnore]
    public Rect Frame => new(X, Y, Width, Height);
}

/// <summary>Window positions of every application at one point in time, keyed by application name.</summary>
public sealed class Snapshot
{
    [JsonPropertyName("apps")]
    public Dictionary<string, List<WindowSnapshot>> Apps { get; init; } = [];

    public void Add(WindowSnapshot window)
    {
        if (!Apps.TryGetValue(window.AppName, out var windows)) Apps[window.AppName] = windows = [];
        windows.Add(window);
    }
}

/// <summary>Snapshots stored under one name: a single snapshot, or a stack when <see cref="IsStack"/> is set.</summary>
public sealed class SnapshotList
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("save-to-disk")]
    public bool SaveToDisk { get; set; }

    [JsonPropertyName("stack")]
    public bool IsStack { get; set; }

    [JsonPropertyName("snapshots")]
    public List<Snapshot> Snapshots { get; init; } = [];

    /// <summary>Pushes on a stack (dropping the oldest beyond <paramref name="maxStackSize"/>, 0 = unlimited) or replaces.</summary>
    public void Add(Snapshot snapshot, int maxStackSize)
    {
        if (!IsStack) Snapshots.Clear();
        Snapshots.Add(snapshot);
        while (IsStack && maxStackSize > 0 && Snapshots.Count > maxStackSize) Snapshots.RemoveAt(0);
    }

    /// <summary>The most recent snapshot, removed from the list when <paramref name="remove"/> is set.</summary>
    public Snapshot? Pop(bool remove)
    {
        if (Snapshots.Count == 0) return null;
        Snapshot last = Snapshots[^1];
        if (remove) Snapshots.RemoveAt(Snapshots.Count - 1);
        return last;
    }
}
