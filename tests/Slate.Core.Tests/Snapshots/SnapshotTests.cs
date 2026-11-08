using Slate.Core.Configuration;
using Slate.Core.Geometry;
using Slate.Core.Operations;
using Slate.Core.Snapshots;

namespace Slate.Core.Tests.Snapshots;

public class SnapshotTests
{
    private static Snapshot With(string app, string title, double x)
    {
        var snapshot = new Snapshot();
        snapshot.Add(new WindowSnapshot(app, title, x, 0, 100, 100));
        return snapshot;
    }

    [Fact]
    public void A_list_keeps_one_snapshot_unless_it_is_a_stack()
    {
        var single = new SnapshotList { Name = "s" };
        single.Add(With("a", "t", 1), 0);
        single.Add(With("a", "t", 2), 0);
        Assert.Single(single.Snapshots);

        var stack = new SnapshotList { Name = "s", IsStack = true };
        for (int i = 0; i < 5; i++) stack.Add(With("a", "t", i), maxStackSize: 3);
        Assert.Equal(new double[] { 2, 3, 4 }, stack.Snapshots.Select(s => s.Apps["a"][0].X));
        Assert.Equal(4, stack.Pop(remove: true)!.Apps["a"][0].X);
        Assert.Equal(3, stack.Pop(remove: false)!.Apps["a"][0].X);
        Assert.Equal(2, stack.Snapshots.Count);
    }

    [Fact]
    public void Only_snapshots_saved_to_disk_are_persisted_and_reloaded()
    {
        var store = new MemorySnapshotStore();
        var repository = new SnapshotRepository(store);
        repository.Add("kept", With("Editor", "doc", 42), saveToDisk: true, isStack: true, maxStackSize: 0);
        repository.Add("memory", With("Editor", "doc", 1), saveToDisk: false, isStack: false, maxStackSize: 0);

        Assert.Contains("\"app-name\":\"Editor\"", store.Json);
        Assert.Contains("\"save-to-disk\":true", store.Json);
        var reloaded = new SnapshotRepository(store);
        reloaded.Load();
        Assert.True(reloaded.Has("kept"));
        Assert.False(reloaded.Has("memory"));
        Assert.True(reloaded.Lists["kept"].IsStack);
        Assert.Equal(42, reloaded.Pop("kept", remove: false)!.Apps["Editor"][0].X);
    }

    [Fact]
    public void Corrupt_snapshot_files_throw()
    {
        var repository = new SnapshotRepository(new MemorySnapshotStore { Json = "[1,2" });
        Assert.ThrowsAny<System.Text.Json.JsonException>(repository.Load);
    }

    [Fact]
    public void Delete_pops_or_removes_everything()
    {
        var repository = new SnapshotRepository(new MemorySnapshotStore());
        repository.Add("s", With("a", "t", 1), false, isStack: true, 0);
        repository.Add("s", With("a", "t", 2), false, isStack: true, 0);
        repository.Delete("s", popOnly: true);
        Assert.Single(repository.Lists["s"].Snapshots);
        repository.Delete("s", popOnly: false);
        Assert.False(repository.Has("s"));
    }

    [Fact]
    public void Snapshot_operations_record_and_restore_titled_windows()
    {
        var env = new TestEnvironment();
        var app = env.Windows.AddApp("Editor");
        var doc = app.AddWindow("notes.txt - Editor", new Rect(10, 20, 300, 400));
        app.AddWindow("", new Rect(0, 0, 1, 1));

        env.Parse("snapshot work save-to-disk;stack").Execute(env.Context);
        Assert.Single(env.Snapshots.Lists["work"].Snapshots[0].Apps["Editor"]);
        Assert.True(env.Snapshots.Lists["work"].SaveToDisk);

        doc.Frame = new Rect(500, 500, 100, 100);
        doc.Title = "notes.txt* - Editor";
        env.Parse("activate-snapshot work delete").Execute(env.Context);
        Assert.Equal(new Rect(10, 20, 300, 400), doc.Frame);
        Assert.False(env.Snapshots.Has("work"));
    }

    [Theory]
    [InlineData(SnapshotTitleMatch.Levenshtein, "readme.mx - Code", "readme.md - Code")]
    [InlineData(SnapshotTitleMatch.Sequential, "readme.txt (2)", "readme.txt")]
    public void Windows_match_the_closest_saved_title(SnapshotTitleMatch match, string title, string expected)
    {
        var saved = new List<WindowSnapshot>
        {
            new("Code", "readme.txt", 0, 0, 1, 1),
            new("Code", "readme.md - Code", 0, 0, 1, 1),
        };
        Assert.Equal(expected, ActivateSnapshotOperation.BestMatch(title, saved, match).Title);
    }

    [Fact]
    public void Title_matching_can_be_configured_per_application()
    {
        var env = new TestEnvironment();
        env.Settings.SetForApp(ConfigKeys.SnapshotTitleMatch, "Code", "sequential");
        Assert.Equal("sequential", env.Settings.GetForApp(ConfigKeys.SnapshotTitleMatch, "Code"));
        Assert.Equal("levenshtein", env.Settings.GetForApp(ConfigKeys.SnapshotTitleMatch, "Other"));
    }
}
