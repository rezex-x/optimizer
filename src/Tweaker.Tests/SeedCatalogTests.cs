using System;
using System.IO;
using System.Linq;
using Xunit;
using Tweaker.Core.Json;
using Tweaker.Core.Models;

namespace Tweaker.Tests;

public class SeedCatalogTests
{
    [Fact]
    public void Seed_catalog_parses_and_has_expected_ids()
    {
        string path = Path.Combine(FindRepoRoot(), "catalog.json");
        List<Tweak> tweaks = TweakJson.DeserializeCatalog(File.ReadAllText(path));

        Assert.Equal(145, tweaks.Count);
        Assert.Contains(tweaks, t => t.Id == "disable-gamedvr");
        Assert.Contains(tweaks, t => t.Id == "disable-vbs" && t.Risk == RiskLevel.High && t.Warning != null);
        Assert.Contains(tweaks, t => t.Id == "disable-mouse-accel" && t.Category == "Input");
        Assert.Contains(tweaks, t => t.Id == "disable-sysmain" && t.Detect.Count == 1);
        Assert.All(tweaks, t => Assert.False(string.IsNullOrWhiteSpace(t.Category)));
    }

    [Fact]
    public void Privacy_winutil_tweaks_present()
    {
        List<Tweak> t = TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(FindRepoRoot(), "catalog.json")));
        foreach (string id in new[] { "consumerfeatures-off","store-search-off","location-tracking-off",
            "delivery-optimization-off","wpbt-off","device-metadata-off","background-apps-off","windows-ai-off","adobe-net-block" })
            Assert.Contains(t, x => x.Id == id && x.Category == "Privacy");
    }

    [Fact]
    public void System_winutil_tweaks_present()
    {
        List<Tweak> t = TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(FindRepoRoot(), "catalog.json")));
        foreach (string id in new[] { "services-manual","bitlocker-off","reserved-storage-off",
            "storage-sense-off","explorer-home-gallery-off","explorer-autodiscovery-off","clock-utc" })
            Assert.Contains(t, x => x.Id == id && x.Category == "System");
        Assert.Contains(t, x => x.Id == "bitlocker-off" && x.Risk == RiskLevel.High && x.Warning != null);
    }

    [Fact]
    public void Debloat_winutil_tweaks_present()
    {
        List<Tweak> t = TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(FindRepoRoot(), "catalog.json")));
        foreach (string id in new[] { "remove-edge","remove-onedrive","edge-debloat","brave-debloat","razer-block","logi-block" })
            Assert.Contains(t, x => x.Id == id && x.Category == "Debloat");
        Assert.Contains(t, x => x.Id == "remove-edge" && x.Risk == RiskLevel.High && x.Warning != null);
        Assert.Contains(t, x => x.Id == "remove-onedrive" && x.Risk == RiskLevel.High && x.Warning != null);
    }

    [Fact]
    public void Network_winutil_tweaks_present()
    {
        List<Tweak> t = TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(FindRepoRoot(), "catalog.json")));
        foreach (string id in new[] { "ipv4-prefer","ipv6-off","teredo-off","rdp-unsigned-warning-off",
            "dns-cloudflare","dns-google","dns-quad9","dns-dhcp-reset" })
            Assert.Contains(t, x => x.Id == id && x.Category == "Network");
    }

    [Fact]
    public void Interface_winutil_A_present()
    {
        List<Tweak> t = TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(FindRepoRoot(), "catalog.json")));
        var expected = new Dictionary<string, string>
        {
            ["end-task-taskbar"] = "Taskbar",
            ["notifications-off"] = "Notifications",
            ["bsod-verbose"] = "Personalization",
            ["battery-percentage"] = "Personalization",
            ["verbose-logon"] = "Personalization",
            ["new-outlook-off"] = "Personalization",
            ["scrollbars-always"] = "Personalization",
            ["mpo-off"] = "Graphics",
            ["numlock-on-boot"] = "Personalization",
            ["window-snapping-off"] = "Personalization",
        };
        foreach ((string id, string category) in expected)
            Assert.Contains(t, x => x.Id == id && x.Category == category);
    }

    [Fact]
    public void Interface_winutil_B_present()
    {
        List<Tweak> t = TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(FindRepoRoot(), "catalog.json")));
        var expected = new Dictionary<string, string>
        {
            ["settings-home-off"] = "Explorer",
            ["bing-search-off"] = "Start Menu",
            ["login-blur-off"] = "Personalization",
            ["lockscreen-off"] = "Personalization",
            ["start-recommendations-off"] = "Start Menu",
            ["taskbar-search-hide"] = "Taskbar",
            ["start-menu-classic"] = "Start Menu",
            ["s0-network-standby"] = "Power",
            ["s3-sleep"] = "Power",
        };
        foreach ((string id, string category) in expected)
            Assert.Contains(t, x => x.Id == id && x.Category == category);
    }

    [Fact]
    public void RunOnce_maintenance_tweaks_are_well_formed()
    {
        List<Tweak> t = TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(FindRepoRoot(), "catalog.json")));
        foreach (string id in new[] { "disk-cleanup", "delete-temp-files" })
        {
            Tweak x = Assert.Single(t, y => y.Id == id);
            Assert.True(x.RunOnce);
            Assert.Equal(TweakType.Batch, x.Type);
            Assert.Equal("Maintenance", x.Category);
            Assert.False(string.IsNullOrWhiteSpace(x.ApplyScript));
            Assert.True(string.IsNullOrWhiteSpace(x.RevertScript));
        }
    }

    [Fact]
    public void Every_tweak_has_benefit_and_downsides()
    {
        string path = Path.Combine(FindRepoRoot(), "catalog.json");
        List<Tweak> tweaks = TweakJson.DeserializeCatalog(File.ReadAllText(path));
        Assert.All(tweaks, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Benefit), $"{t.Id} missing benefit");
            Assert.False(string.IsNullOrWhiteSpace(t.Downsides), $"{t.Id} missing downsides");
        });
    }

    [Fact]
    public void Ids_are_unique()
    {
        string path = Path.Combine(FindRepoRoot(), "catalog.json");
        List<Tweak> tweaks = TweakJson.DeserializeCatalog(File.ReadAllText(path));
        Assert.Equal(tweaks.Count, tweaks.Select(t => t.Id).Distinct().Count());
    }

    [Fact]
    public void All_tweaks_have_sane_ops_and_no_winutil_runtime_refs()
    {
        List<Tweak> tweaks = TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(FindRepoRoot(), "catalog.json")));
        var hives = new HashSet<string> { "HKLM","HKCU","HKCR","HKU","HKCC" };
        foreach (Tweak t in tweaks)
        {
            foreach (RegistryOp op in t.Apply.Concat(t.Revert).Concat(t.Detect))
            {
                Assert.Contains(op.Hive, hives);
                Assert.False(string.IsNullOrWhiteSpace(op.Path));
                Assert.Contains(op.Kind, new[] { "DWord","QWord","String","ExpandString","MultiString","Binary" });
            }
            if (t.Type == TweakType.Batch)
                Assert.False(string.IsNullOrWhiteSpace(t.ApplyScript), $"{t.Id} batch has no applyScript");
            if (t.RunOnce) Assert.Equal(TweakType.Batch, t.Type);
            if (t.Type == TweakType.Value)
            {
                Assert.NotNull(t.Power);
                Assert.True(Guid.TryParse(t.Power!.Subgroup, out _), $"{t.Id} subgroup");
                Assert.True(Guid.TryParse(t.Power.Setting, out _), $"{t.Id} setting");
                Assert.True(t.Power.Options.Count > 0 || t.Power.Min < t.Power.Max, $"{t.Id} range");
                Assert.True(t.Power.Step >= 1, $"{t.Id} step");
                Assert.Equal(t.Power.Options.Count, t.Power.Options.Select(o => o.Value).Distinct().Count());
                Assert.Empty(t.Apply);
                Assert.Null(t.ApplyScript);
            }
            else
            {
                Assert.Null(t.Power);
            }
            foreach (string? s in new[] { t.ApplyScript, t.RevertScript })
                if (s != null)
                {
                    Assert.DoesNotContain("Invoke-WinUtil", s);
                    Assert.DoesNotContain("$sync", s);
                }
        }
        foreach (string id in new[] { "bitlocker-off", "remove-edge", "remove-onedrive", "disable-vbs" })
            Assert.Contains(tweaks, x => x.Id == id && x.Warning != null);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "catalog.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("catalog.json not found up the tree");
    }
}
