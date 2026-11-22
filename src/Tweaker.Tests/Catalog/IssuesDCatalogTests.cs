using System;
using System.IO;
using System.Linq;
using Tweaker.Core.Catalog;
using Tweaker.Core.Json;
using Tweaker.Core.Models;
using Tweaker.Core.Nvidia;
using Xunit;

namespace Tweaker.Tests.Catalog;

public class IssuesDCatalogTests
{
    private static List<Tweak> Catalog() =>
        TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalog.json")));

    [Theory]
    [InlineData("disable-sysmain", 100)]
    [InlineData("disable-search-indexing", 60)]
    [InlineData("background-apps-off", 150)]
    [InlineData("windows-ai-off", 100)]
    [InlineData("tb-widgets", 150)]
    [InlineData("disable-gamedvr", 30)]
    public void Tweaks_that_free_memory_carry_an_estimate(string id, int mb)
    {
        Assert.Equal(mb, Assert.Single(Catalog(), t => t.Id == id).FreesRamMb);
    }

    [Fact]
    public void Only_those_tweaks_carry_an_estimate()
    {
        Assert.Equal(6, Catalog().Count(t => t.FreesRamMb is not null));
    }

    [Theory]
    [InlineData("disable-telemetry")]
    [InlineData("disable-cortana")]
    [InlineData("delivery-optimization-off")]
    [InlineData("xbox-services-off")]
    [InlineData("nvidia-telemetry-off")]
    public void Tweaks_that_normally_stop_nothing_carry_no_estimate(string id)
    {
        Assert.Null(Assert.Single(Catalog(), t => t.Id == id).FreesRamMb);
    }

    [Fact]
    public void Only_sysmain_and_search_indexing_cover_a_service()
    {
        Assert.Equal(new[] { "SysMain" }, FreedCoverage.ServicesFor("disable-sysmain"));
        Assert.Equal(new[] { "WSearch" }, FreedCoverage.ServicesFor("disable-search-indexing"));
        Assert.Empty(FreedCoverage.ServicesFor("background-apps-off"));
        Assert.Empty(FreedCoverage.ServicesFor("xbox-services-off"));
        Assert.All(Catalog().Where(t => t.FreesRamMb is null), t => Assert.Empty(FreedCoverage.ServicesFor(t.Id)));
    }

    [Theory]
    [InlineData("vfx-minimize-animation", "Control Panel\\Desktop\\WindowMetrics", "MinAnimate", "String")]
    [InlineData("vfx-taskbar-animations", "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarAnimations", "DWord")]
    [InlineData("vfx-peek", "Software\\Microsoft\\Windows\\DWM", "EnableAeroPeek", "DWord")]
    [InlineData("vfx-selection-rectangle", "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ListviewAlphaSelect", "DWord")]
    [InlineData("vfx-icon-label-shadows", "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ListviewShadow", "DWord")]
    [InlineData("vfx-drag-full-windows", "Control Panel\\Desktop", "DragFullWindows", "String")]
    [InlineData("vfx-thumbnails", "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "IconsOnly", "DWord")]
    public void Visual_effects_live_on_the_config_tab_with_explicit_defaults(string id, string path, string name, string kind)
    {
        Tweak t = Assert.Single(Catalog(), x => x.Id == id);
        Assert.Equal(("Visual Effects", TweakType.Registry, RiskLevel.Low, true), (t.Category, t.Type, t.Risk, t.RequiresRestart));
        RegistryOp apply = Assert.Single(t.Apply);
        RegistryOp revert = Assert.Single(t.Revert);
        Assert.Equal(("HKCU", path, name, kind), (apply.Hive, apply.Path, apply.Name, apply.Kind));
        Assert.Equal((apply.Hive, apply.Path, apply.Name), (revert.Hive, revert.Path, revert.Name));
        Assert.NotEqual(apply.Value.ToString(), revert.Value.ToString());
        Assert.Contains("sign out", t.Description);
        Assert.True(ConfigSurface.IsConfig(t.Category));
    }

    [Fact]
    public void Every_nvidia_setting_has_a_category()
    {
        var allowed = new[] { "Performance", "Latency", "Sync", "Image quality" };
        Assert.All(NvidiaSettingsCatalog.Items(), s => Assert.Contains(s.Category, allowed));
        Assert.Equal("Latency", NvidiaSettingsCatalog.Items().Single(s => s.Id == "low-latency").Category);
    }
}
