using System.IO;
using Tweaker.Core.Nvidia;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Nvidia;

public class NvidiaBackupStoreTests
{
    private static string PathIn(TempDir d) => Path.Combine(d.Path, "nvidia-backup.json");

    [Fact]
    public void Stores_and_reads_prior_across_instances()
    {
        using var dir = new TempDir();
        new NvidiaBackupStore(PathIn(dir)).StorePriorIfAbsent(0x1057EB71, 5);
        Assert.Equal(5u, new NvidiaBackupStore(PathIn(dir)).GetPrior(0x1057EB71));
    }

    [Fact]
    public void Missing_key_is_null()
    {
        using var dir = new TempDir();
        Assert.Null(new NvidiaBackupStore(PathIn(dir)).GetPrior(0x999));
    }

    [Fact]
    public void StorePriorIfAbsent_keeps_the_first_value()
    {
        using var dir = new TempDir();
        var store = new NvidiaBackupStore(PathIn(dir));
        store.StorePriorIfAbsent(0x1057EB71, 5);
        store.StorePriorIfAbsent(0x1057EB71, 1);
        Assert.Equal(5u, store.GetPrior(0x1057EB71));
    }

    [Fact]
    public void Null_prior_is_not_stored()
    {
        using var dir = new TempDir();
        var store = new NvidiaBackupStore(PathIn(dir));
        store.StorePriorIfAbsent(0x1057EB71, null);
        Assert.Null(store.GetPrior(0x1057EB71));
    }
}
