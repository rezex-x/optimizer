using System.Linq;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class InterruptRulesTests
{
    private const string Gpu = @"PCI\VEN_10DE&DEV_2803\4&1";

    [Fact]
    public void Only_pci_devices_have_interrupt_settings_and_their_paths_live_under_enum()
    {
        Assert.True(InterruptRules.IsInterruptCapable(Gpu));
        Assert.True(InterruptRules.IsInterruptCapable(@"pci\ven_8086"));
        Assert.False(InterruptRules.IsInterruptCapable(@"USB\VID_046D&PID_C547\6"));
        Assert.False(InterruptRules.IsInterruptCapable(@"HID\VID_046D"));
        Assert.Equal(@"SYSTEM\CurrentControlSet\Enum\" + Gpu + @"\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties",
            InterruptRules.MsiPath(Gpu));
        Assert.Equal(@"SYSTEM\CurrentControlSet\Enum\" + Gpu + @"\Device Parameters\Interrupt Management\Affinity Policy",
            InterruptRules.AffinityPath(Gpu));
    }

    [Fact]
    public void Affinity_options_are_default_spread_and_one_per_cpu()
    {
        var options = InterruptRules.AffinityOptions(4);

        Assert.Equal(new[] { "Windows default", "Spread across all cores", "CPU 0", "CPU 1", "CPU 2", "CPU 3" }, options.Select(o => o.Label));
        Assert.Equal((-1, 0UL), (options[0].Policy, options[0].Mask));
        Assert.Equal((5, 0UL), (options[1].Policy, options[1].Mask));
        Assert.Equal(new ulong[] { 1, 2, 4, 8 }, options.Skip(2).Select(o => o.Mask));
        Assert.All(options.Skip(2), o => Assert.Equal(4, o.Policy));
        Assert.Equal(2 + 64, InterruptRules.AffinityOptions(128).Count);
    }

    [Fact]
    public void Stored_policies_match_an_option_or_become_custom()
    {
        var options = InterruptRules.AffinityOptions(4);

        Assert.Same(options[0], InterruptRules.Match(options, -1, 0));
        Assert.Same(options[0], InterruptRules.Match(options, 0, 0));
        Assert.Same(options[1], InterruptRules.Match(options, 5, 0));
        Assert.Same(options[4], InterruptRules.Match(options, 4, 4));
        Assert.Equal(("Custom (policy 4)", 6UL), (InterruptRules.Match(options, 4, 6).Label, InterruptRules.Match(options, 4, 6).Mask));
        Assert.Equal("Custom (policy 3)", InterruptRules.Match(options, 3, 0).Label);
    }

    [Fact]
    public void Masks_and_backup_texts_round_trip()
    {
        byte[] bytes = InterruptRules.MaskBytes(1UL << 9);
        Assert.Equal(8, bytes.Length);
        Assert.Equal(2, bytes[1]);
        Assert.Equal(1UL << 9, InterruptRules.MaskFrom(bytes));
        Assert.Equal(0UL, InterruptRules.MaskFrom(null));
        Assert.Equal(4UL, InterruptRules.MaskFrom(new byte[] { 4, 0, 0, 0 }));

        Assert.Equal("", InterruptRules.AffinityText(-1, 0));
        Assert.Equal("", InterruptRules.AffinityText(0, 0));
        Assert.Equal("4:10", InterruptRules.AffinityText(4, 0x10));
        Assert.Equal((4, 16UL), InterruptRules.ParseAffinity("4:10"));
        Assert.Equal((5, 0UL), InterruptRules.ParseAffinity("5:0"));
        Assert.Equal((-1, 0UL), InterruptRules.ParseAffinity(""));
        Assert.Equal((-1, 0UL), InterruptRules.ParseAffinity("junk"));

        Assert.Equal(("", "1", "0"), (InterruptRules.MsiText(MsiMode.Default), InterruptRules.MsiText(MsiMode.On), InterruptRules.MsiText(MsiMode.Off)));
        Assert.Equal((MsiMode.Default, MsiMode.On, MsiMode.Off), (InterruptRules.ParseMsi(""), InterruptRules.ParseMsi("1"), InterruptRules.ParseMsi("0")));
    }

    [Fact]
    public void Nic_keywords_have_hints_and_a_stable_order()
    {
        Assert.True(NicKeywords.IsSupported("*EEE"));
        Assert.True(NicKeywords.IsSupported("enablegreenethernet"));
        Assert.False(NicKeywords.IsSupported("*ReceiveBuffers"));
        Assert.Contains("wake-up latency", NicKeywords.HintFor("*EEE"));
        Assert.Contains("Keep on", NicKeywords.HintFor("*InterruptModeration"));
        Assert.Equal("", NicKeywords.HintFor("*Unknown"));
        Assert.True(NicKeywords.Order("*EEE") < NicKeywords.Order("*InterruptModeration"));
        Assert.True(NicKeywords.Order("*RSS") < NicKeywords.Order("*Unknown"));
    }
}
