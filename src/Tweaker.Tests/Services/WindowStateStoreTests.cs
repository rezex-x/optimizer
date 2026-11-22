using System.IO;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class WindowStateStoreTests
{
    [Fact]
    public void Load_returns_null_when_no_file()
    {
        using var temp = new TempDir();
        var store = new WindowStateStore(Path.Combine(temp.Path, "window.json"));
        Assert.Null(store.Load());
    }

    [Fact]
    public void Save_then_load_roundtrips()
    {
        using var temp = new TempDir();
        string path = Path.Combine(temp.Path, "sub", "window.json");
        var store = new WindowStateStore(path);

        store.Save(new WindowPlacement(120, 60, 1000, 720, Maximized: true));
        var p = store.Load();

        Assert.NotNull(p);
        Assert.Equal(120, p!.Left);
        Assert.Equal(60, p.Top);
        Assert.Equal(1000, p.Width);
        Assert.Equal(720, p.Height);
        Assert.True(p.Maximized);
    }

    [Fact]
    public void Load_returns_null_on_corrupt_file()
    {
        using var temp = new TempDir();
        string path = Path.Combine(temp.Path, "window.json");
        File.WriteAllText(path, "{ not valid json");
        Assert.Null(new WindowStateStore(path).Load());
    }

    [Theory]
    [InlineData(100, 100, 1080, 700, 0, 0, 1920, 1080, true)]
    [InlineData(3000, 100, 1080, 700, 0, 0, 1920, 1080, false)]
    [InlineData(100, -650, 1080, 700, 0, 0, 1920, 1080, true)]
    [InlineData(100, 100, 0, 0, 0, 0, 1920, 1080, false)]
    public void IsReasonablyOnScreen_matches_expected(
        double l, double t, double w, double h,
        double sl, double st, double sw, double sh, bool expected)
    {
        var p = new WindowPlacement(l, t, w, h, false);
        Assert.Equal(expected, WindowStateStore.IsReasonablyOnScreen(p, sl, st, sw, sh));
    }
}
