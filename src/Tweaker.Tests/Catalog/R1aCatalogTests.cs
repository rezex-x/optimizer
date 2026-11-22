using System;
using System.IO;
using System.Linq;
using Tweaker.Core.Json;
using Tweaker.Core.Models;
using Xunit;

namespace Tweaker.Tests.Catalog;

public class R1aCatalogTests
{
    private static List<Tweak> Catalog() =>
        TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalog.json")));

    [Theory]
    [InlineData("raw-mouse-throttle-off", "Input", TweakType.Registry, RiskLevel.Low, false)]
    [InlineData("global-timer-resolution", "CPU", TweakType.Registry, RiskLevel.Low, true)]
    [InlineData("hypervisor-off", "System", TweakType.Batch, RiskLevel.High, true)]
    [InlineData("click-to-do-off", "Privacy", TweakType.Registry, RiskLevel.Low, false)]
    [InlineData("paint-ai-off", "Privacy", TweakType.Registry, RiskLevel.Low, false)]
    [InlineData("lockscreen-tips-off", "Personalization", TweakType.Registry, RiskLevel.Low, false)]
    [InlineData("explorer-recommended-off", "Explorer", TweakType.Registry, RiskLevel.Low, false)]
    [InlineData("clipboard-actions-off", "Personalization", TweakType.Registry, RiskLevel.Low, false)]
    [InlineData("telemetry-tasks-off", "Privacy", TweakType.Batch, RiskLevel.Low, false)]
    [InlineData("xbox-services-off", "Gaming", TweakType.Batch, RiskLevel.Medium, false)]
    [InlineData("bluetooth-service-off", "System", TweakType.Batch, RiskLevel.High, false)]
    [InlineData("cdp-service-off", "Privacy", TweakType.Batch, RiskLevel.Medium, false)]
    [InlineData("nvidia-telemetry-off", "Graphics", TweakType.Batch, RiskLevel.Low, false)]
    [InlineData("tdr-delay", "Graphics", TweakType.Registry, RiskLevel.Medium, true)]
    [InlineData("nvidia-pstate-lock", "Graphics", TweakType.Batch, RiskLevel.High, true)]
    [InlineData("rsc-off", "Network", TweakType.Batch, RiskLevel.Low, false)]
    [InlineData("ecn-on", "Network", TweakType.Batch, RiskLevel.Low, false)]
    [InlineData("disable-8dot3", "System", TweakType.Batch, RiskLevel.Low, false)]
    [InlineData("ntfs-memory-usage", "System", TweakType.Batch, RiskLevel.Low, true)]
    [InlineData("scheduled-optimize-off", "System", TweakType.Batch, RiskLevel.Low, false)]
    [InlineData("bulk-app-removal", "Debloat", TweakType.Batch, RiskLevel.Medium, false)]
    public void Each_new_tweak_is_filed_and_rated(string id, string category, TweakType type, RiskLevel risk, bool restart)
    {
        Tweak t = Assert.Single(Catalog(), x => x.Id == id);
        Assert.Equal((category, type, risk, restart), (t.Category, t.Type, t.Risk, t.RequiresRestart));
        Assert.False(string.IsNullOrWhiteSpace(t.Name));
        Assert.False(string.IsNullOrWhiteSpace(t.Description));
        Assert.False(string.IsNullOrWhiteSpace(t.Benefit));
        Assert.False(string.IsNullOrWhiteSpace(t.Downsides));
        if (type == TweakType.Registry) Assert.NotEmpty(t.Apply);
        else Assert.False(string.IsNullOrWhiteSpace(t.ApplyScript));
    }

    [Fact]
    public void Switchable_batch_tweaks_can_be_reverted_and_the_app_removal_is_run_once()
    {
        string[] switchable =
        {
            "hypervisor-off", "telemetry-tasks-off", "xbox-services-off", "bluetooth-service-off", "cdp-service-off",
            "nvidia-telemetry-off", "nvidia-pstate-lock", "rsc-off", "ecn-on", "disable-8dot3", "ntfs-memory-usage",
            "scheduled-optimize-off",
        };
        foreach (string id in switchable)
        {
            Tweak t = Assert.Single(Catalog(), x => x.Id == id);
            Assert.False(t.RunOnce, id);
            Assert.False(string.IsNullOrWhiteSpace(t.RevertScript), id);
        }

        Tweak removal = Assert.Single(Catalog(), x => x.Id == "bulk-app-removal");
        Assert.True(removal.RunOnce);
        Assert.Null(removal.RevertScript);
    }

    [Fact]
    public void High_risk_and_feature_breaking_tweaks_carry_a_warning()
    {
        foreach (string id in new[] { "hypervisor-off", "bluetooth-service-off", "nvidia-pstate-lock", "xbox-services-off", "bulk-app-removal" })
            Assert.False(string.IsNullOrWhiteSpace(Assert.Single(Catalog(), x => x.Id == id).Warning), id);
    }

    [Fact]
    public void Key_values_match_the_research()
    {
        List<Tweak> c = Catalog();
        RegistryOp mouse = Assert.Single(Assert.Single(c, x => x.Id == "raw-mouse-throttle-off").Apply);
        Assert.Equal(("Control Panel\\Mouse", "RawMouseThrottleEnabled"), (mouse.Path, mouse.Name));
        RegistryOp timer = Assert.Single(Assert.Single(c, x => x.Id == "global-timer-resolution").Apply);
        Assert.Equal("GlobalTimerResolutionRequests", timer.Name);
        Assert.Contains("hypervisorlaunchtype off", Assert.Single(c, x => x.Id == "hypervisor-off").ApplyScript);
        Assert.DoesNotContain("XboxGipSvc", Assert.Single(c, x => x.Id == "xbox-services-off").ApplyScript);
        string removal = Assert.Single(c, x => x.Id == "bulk-app-removal").ApplyScript!;
        Assert.Contains("Remove-AppxProvisionedPackage", removal);
        Assert.DoesNotContain("WindowsStore", removal);
        Assert.DoesNotContain("XboxSpeechToTextOverlay", removal);
    }

    [Fact]
    public void Overstated_descriptions_are_honest()
    {
        List<Tweak> c = Catalog();
        Assert.Contains("rarely adds fps", Assert.Single(c, x => x.Id == "ultimate-power").Description);
        Assert.Contains("within noise", Assert.Single(c, x => x.Id == "priority-separation").Description);
    }

    [Theory]
    [InlineData("xbox-services-off", 3)]
    [InlineData("bluetooth-service-off", 1)]
    [InlineData("cdp-service-off", 1)]
    [InlineData("disable-8dot3", 1)]
    [InlineData("ntfs-memory-usage", 1)]
    public void State_based_batch_tweaks_detect_the_real_system_state(string id, int probes)
    {
        Assert.Equal(probes, Assert.Single(Catalog(), x => x.Id == id).Detect.Count);
    }

    [Fact]
    public void New_registry_tweaks_revert_to_the_windows_default_without_a_backup()
    {
        foreach (string id in new[] { "raw-mouse-throttle-off", "global-timer-resolution", "click-to-do-off", "paint-ai-off",
                     "lockscreen-tips-off", "explorer-recommended-off", "clipboard-actions-off", "tdr-delay" })
        {
            Tweak t = Assert.Single(Catalog(), x => x.Id == id);
            Assert.Equal(t.Apply.Select(o => (o.Hive, o.Path, o.Name)), t.Revert.Select(o => (o.Hive, o.Path, o.Name)));
        }
        Assert.Contains("MareBackup", Assert.Single(Catalog(), x => x.Id == "telemetry-tasks-off").ApplyScript);
    }
}
