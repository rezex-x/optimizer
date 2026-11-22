using System.Linq;
using Tweaker.Core.Nvidia;
using Xunit;

namespace Tweaker.Tests.Nvidia;

public class NvidiaCatalogTests
{
    [Fact]
    public void SettingIds_are_unique()
    {
        var items = NvidiaSettingsCatalog.Items();
        Assert.Equal(items.Count, items.Select(s => s.SettingId).Distinct().Count());
    }

    [Theory]
    [InlineData("ogl-triple-buffer", 0x20FDD1F9u)]
    [InlineData("aniso-sample-optimization", 0x00E73211u)]
    [InlineData("negative-lod-bias-clamp", 0x0019BB68u)]
    public void New_settings_present_with_expected_id(string id, uint settingId)
    {
        var s = NvidiaSettingsCatalog.Items().SingleOrDefault(x => x.Id == id);
        Assert.NotNull(s);
        Assert.Equal(settingId, s!.SettingId);
        Assert.False(string.IsNullOrWhiteSpace(s.Benefit));
        Assert.False(string.IsNullOrWhiteSpace(s.Downsides));
    }
}
