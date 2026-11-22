using System.IO;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class AppliedTweakStoreTests
{
    [Fact]
    public void Missing_file_reads_as_nothing_applied()
    {
        using var temp = new TempDir();
        var store = new AppliedTweakStore(Path.Combine(temp.Path, "none", "applied.json"));
        Assert.False(store.IsApplied("x"));
    }

    [Fact]
    public void SetApplied_round_trips_through_the_file()
    {
        using var temp = new TempDir();
        string file = Path.Combine(temp.Path, "sub", "applied.json");

        new AppliedTweakStore(file).SetApplied("a", true);
        var reloaded = new AppliedTweakStore(file);
        Assert.True(reloaded.IsApplied("a"));

        reloaded.SetApplied("a", false);
        Assert.False(new AppliedTweakStore(file).IsApplied("a"));
    }

    [Fact]
    public void Corrupt_file_reads_as_nothing_applied_and_is_repaired_on_the_next_write()
    {
        using var temp = new TempDir();
        string file = Path.Combine(temp.Path, "applied.json");
        File.WriteAllText(file, "{ not json");

        var store = new AppliedTweakStore(file);
        Assert.False(store.IsApplied("a"));

        store.SetApplied("a", true);
        Assert.True(new AppliedTweakStore(file).IsApplied("a"));
    }

    [Fact]
    public void InMemory_store_tracks_applied_ids()
    {
        var store = new InMemoryAppliedTweakStore();
        store.SetApplied("a", true);
        Assert.True(store.IsApplied("a"));
        store.SetApplied("a", false);
        Assert.False(store.IsApplied("a"));
    }
}
