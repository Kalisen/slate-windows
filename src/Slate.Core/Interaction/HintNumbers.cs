namespace Slate.Core.Interaction;

/// <summary>
/// Gives each window a stable number for <c>windowHintsOrder persist</c>: a window keeps its hint while it lives, and the
/// numbers of closed windows are reused lowest first.
/// </summary>
public sealed class HintNumbers
{
    private readonly Dictionary<long, int> numbers = [];
    private readonly SortedSet<int> freed = [];
    private int next;

    public int NumberFor(long windowId)
    {
        if (numbers.TryGetValue(windowId, out int number)) return number;
        if (freed.Count > 0)
        {
            number = freed.Min;
            freed.Remove(number);
        }
        else
        {
            number = next++;
        }
        numbers[windowId] = number;
        return number;
    }

    /// <summary>Frees the numbers of windows that no longer exist.</summary>
    public void Retain(IEnumerable<long> liveWindowIds)
    {
        var live = liveWindowIds.ToHashSet();
        foreach (var (id, number) in numbers.Where(p => !live.Contains(p.Key)).ToList())
        {
            numbers.Remove(id);
            freed.Add(number);
        }
    }
}
