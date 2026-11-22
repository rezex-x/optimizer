using System;
using System.IO;
using System.Linq;
using Tweaker.Core.Json;
using Tweaker.Core.Models;
using Xunit;

namespace Tweaker.Tests.Catalog;

public class R1bCatalogTests
{
    private const string Processor = "54533251-82be-4824-96c1-47b60b740d00";

    private static List<Tweak> Catalog() =>
        TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalog.json")));

    [Theory]
    [InlineData("power-core-parking-min", "CPU", RiskLevel.Low, "0cc5b647-c1df-4637-891a-dec35c318583", 0)]
    [InlineData("power-core-parking-max", "CPU", RiskLevel.Medium, "ea062031-0e34-4ff1-9b6d-eb1059334028", 0)]
    [InlineData("power-min-processor-state", "CPU", RiskLevel.Medium, "893dee8e-2bef-41e0-89c6-b55d0929964c", 0)]
    [InlineData("power-idle-demote", "CPU", RiskLevel.Low, "4b92d758-5a24-4851-a470-815d78aee119", 0)]
    [InlineData("power-idle-promote", "CPU", RiskLevel.Low, "7b224883-b3cc-4d79-819f-8374152cbe7c", 0)]
    [InlineData("power-perf-increase-threshold", "CPU", RiskLevel.Low, "06cadf0e-64ed-448a-8927-ce7bf90eb35d", 0)]
    [InlineData("power-perf-decrease-threshold", "CPU", RiskLevel.Low, "12a0ab44-fe28-4fa9-b3bd-4b64f44960a6", 0)]
    [InlineData("power-perf-check-interval", "CPU", RiskLevel.Low, "4d2b0152-7d5c-498b-88e2-34345392a2c5", 0)]
    [InlineData("power-boost-mode", "CPU", RiskLevel.Medium, "be337238-0d82-4146-a960-4f3749d470c7", 7)]
    [InlineData("power-pcie-aspm", "Power", RiskLevel.Low, "ee12f906-d277-404b-b6da-e5fa1a576df5", 3)]
    [InlineData("power-usb-selective-suspend", "Power", RiskLevel.Low, "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", 2)]
    public void Each_power_setting_is_filed_with_its_guid(string id, string category, RiskLevel risk, string setting, int options)
    {
        Tweak t = Assert.Single(Catalog(), x => x.Id == id);
        Assert.Equal((category, TweakType.Value, risk, false), (t.Category, t.Type, t.Risk, t.RequiresRestart));
        Assert.Equal(setting, t.Power!.Setting);
        Assert.Equal(options, t.Power.Options.Count);
        Assert.False(string.IsNullOrWhiteSpace(t.Description));
        Assert.Contains("plugged in", t.Description);
    }

    [Fact]
    public void Processor_settings_share_the_processor_subgroup_and_percent_ranges()
    {
        List<Tweak> c = Catalog();
        Assert.All(c.Where(t => t.Type == TweakType.Value && t.Category == "CPU"), t => Assert.Equal(Processor, t.Power!.Subgroup));
        foreach (string id in new[] { "power-core-parking-min", "power-core-parking-max", "power-min-processor-state", "power-idle-demote",
                     "power-idle-promote", "power-perf-increase-threshold", "power-perf-decrease-threshold" })
        {
            PowerSettingSpec p = Assert.Single(c, x => x.Id == id).Power!;
            Assert.Equal((0, 100, 1, "%"), (p.Min, p.Max, p.Step, p.Unit));
        }
        PowerSettingSpec interval = Assert.Single(c, x => x.Id == "power-perf-check-interval").Power!;
        Assert.Equal((1, 5000, "ms"), (interval.Min, interval.Max, interval.Unit));
        Assert.Equal(11, c.Count(t => t.Type == TweakType.Value));
    }

    [Fact]
    public void Clock_scaling_settings_admit_the_cpu_may_ignore_them()
    {
        foreach (string id in new[] { "power-perf-increase-threshold", "power-perf-decrease-threshold", "power-perf-check-interval" })
            Assert.Contains("Speed Shift", Assert.Single(Catalog(), x => x.Id == id).Description);
    }

    [Fact]
    public void Settings_with_a_performance_core_twin_say_so_for_hybrid_cpus()
    {
        foreach (string id in new[] { "power-core-parking-min", "power-core-parking-max", "power-min-processor-state",
                     "power-perf-increase-threshold", "power-perf-decrease-threshold" })
            Assert.Contains("hybrid CPUs", Assert.Single(Catalog(), x => x.Id == id).Description);
    }
}
