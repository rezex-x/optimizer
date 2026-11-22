using System.Linq;
using Tweaker.Core.Nvidia;
using Xunit;

namespace Tweaker.Tests.Nvidia;

public class NvidiaSettingsCatalogTests
{
    [Fact]
    public void Catalog_has_nine_well_formed_settings()
    {
        var items = NvidiaSettingsCatalog.Items();
        Assert.Equal(9, items.Count);
        Assert.Equal(items.Count, items.Select(s => s.Id).Distinct().Count());
        foreach (var s in items)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id));
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
            Assert.False(string.IsNullOrWhiteSpace(s.Benefit), $"{s.Id} benefit");
            Assert.False(string.IsNullOrWhiteSpace(s.Downsides), $"{s.Id} downsides");
            Assert.NotEqual(0u, s.SettingId);
            Assert.NotEqual(s.RecommendedValue, s.DefaultValue);
        }
        Assert.Contains(items, s => s.Id == "power-management");
        Assert.Contains(items, s => s.Id == "vsync-off");
    }
}
