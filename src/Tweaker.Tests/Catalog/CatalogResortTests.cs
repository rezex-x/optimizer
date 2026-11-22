using System;
using System.IO;
using System.Linq;
using Tweaker.Core.Catalog;
using Tweaker.Core.Services;
using Xunit;

namespace Tweaker.Tests.Catalog;

public class CatalogResortTests
{
    private static CatalogService RealCatalog()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "catalog.json");
        Assert.True(File.Exists(path), "catalog.json must be copied to the test output");
        return new CatalogService(new FileCatalogSource(path));
    }

    [Fact]
    public void Interface_category_is_gone()
    {
        var svc = RealCatalog();
        Assert.DoesNotContain("Interface", svc.Categories(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Category_set_is_exactly_the_expected_taxonomy()
    {
        var svc = RealCatalog();
        var expected = new[]
        {
            "CPU", "Debloat", "Explorer", "Gaming", "Graphics", "Input", "Maintenance", "Network",
            "Notifications", "Personalization", "Power", "Privacy", "Start Menu", "System", "Taskbar", "Visual Effects",
        };
        Assert.Equal(expected, svc.Categories().OrderBy(c => c, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Config_surface_categories_all_present_and_disjoint_from_tweaks()
    {
        var svc = RealCatalog();
        foreach (string c in ConfigSurface.Categories)
            Assert.Contains(c, svc.Categories(), StringComparer.OrdinalIgnoreCase);

        var tweaksSide = svc.Categories().Where(c => !ConfigSurface.IsConfig(c));
        Assert.DoesNotContain(tweaksSide, ConfigSurface.IsConfig);
    }

    [Theory]
    [InlineData("tb-widgets", "Taskbar")]
    [InlineData("tb-chat", "Taskbar")]
    [InlineData("tb-taskview", "Taskbar")]
    [InlineData("tb-combine", "Taskbar")]
    [InlineData("sm-recentdocs", "Start Menu")]
    [InlineData("sm-morepins", "Start Menu")]
    [InlineData("ex-thispc", "Explorer")]
    [InlineData("ex-compact", "Explorer")]
    [InlineData("notif-tips", "Notifications")]
    [InlineData("notif-suggested", "Notifications")]
    [InlineData("taskbar-left-align", "Taskbar")]
    [InlineData("dark-mode", "Personalization")]
    [InlineData("show-file-extensions", "Explorer")]
    [InlineData("bing-search-off", "Start Menu")]
    [InlineData("notifications-off", "Notifications")]
    [InlineData("mpo-off", "Graphics")]
    [InlineData("s3-sleep", "Power")]
    [InlineData("disable-startup-delay", "System")]
    public void Item_is_in_expected_category(string id, string category)
    {
        var svc = RealCatalog();
        var tweak = svc.Tweaks.SingleOrDefault(t => t.Id == id);
        Assert.NotNull(tweak);
        Assert.Equal(category, tweak!.Category);
    }

    [Fact]
    public void Removed_duplicate_ids_are_absent()
    {
        var svc = RealCatalog();
        Assert.DoesNotContain(svc.Tweaks, t => t.Id == "ex-showext");
        Assert.DoesNotContain(svc.Tweaks, t => t.Id == "ex-hidden");
    }

    [Fact]
    public void No_duplicate_ids()
    {
        var svc = RealCatalog();
        var dupes = svc.Tweaks.GroupBy(t => t.Id).Where(g => g.Count() > 1).Select(g => g.Key);
        Assert.Empty(dupes);
    }

    [Theory]
    [InlineData("power-throttling-off", "System")]
    [InlineData("disable-paging-executive", "System")]
    [InlineData("best-performance-power-mode", "Power")]
    [InlineData("windowed-games-optimization", "Graphics")]
    [InlineData("disable-fast-startup", "Power")]
    [InlineData("enable-trim", "Maintenance")]
    [InlineData("clear-shader-cache", "Maintenance")]
    [InlineData("disable-search-indexing", "System")]
    [InlineData("disable-prefetcher", "System")]
    [InlineData("input-queue-size", "Input")]
    [InlineData("nic-power-saving-off", "Network")]
    public void New_2026_tweaks_present_in_expected_category(string id, string category)
    {
        var tweak = RealCatalog().Tweaks.SingleOrDefault(t => t.Id == id);
        Assert.NotNull(tweak);
        Assert.Equal(category, tweak!.Category);
    }

    [Theory]
    [InlineData("cpu-max-performance", "CPU")]
    [InlineData("cpu-boost-aggressive", "CPU")]
    [InlineData("system-cooling-active", "Power")]
    [InlineData("cpu-idle-disable", "CPU")]
    [InlineData("disable-dynamic-tick", "System")]
    [InlineData("tsc-sync-enhanced", "System")]
    public void Slice1_bios_adjacent_tweaks_present(string id, string category)
    {
        var tweak = RealCatalog().Tweaks.SingleOrDefault(t => t.Id == id);
        Assert.NotNull(tweak);
        Assert.Equal(category, tweak!.Category);
        Assert.Equal(Tweaker.Core.Models.TweakType.Batch, tweak.Type);
        Assert.False(string.IsNullOrWhiteSpace(tweak.ApplyScript));
    }

    [Theory]
    [InlineData("cpu-idle-disable")]
    [InlineData("disable-dynamic-tick")]
    [InlineData("tsc-sync-enhanced")]
    public void Slice1_high_risk_tweaks_carry_a_warning(string id)
    {
        var tweak = RealCatalog().Tweaks.Single(t => t.Id == id);
        Assert.Equal(Tweaker.Core.Models.RiskLevel.High, tweak.Risk);
        Assert.False(string.IsNullOrWhiteSpace(tweak.Warning));
    }
}
