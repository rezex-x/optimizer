using System.Linq;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ProfileRulesTests
{
    [Fact]
    public void Exe_names_normalize_to_the_file_name_with_exe()
    {
        Assert.Equal("obs64.exe", ProfileRules.NormalizeExe(" obs64 "));
        Assert.Equal("FortniteClient-Win64-Shipping.exe",
            ProfileRules.NormalizeExe(@"""C:\Program Files\Epic Games\Fortnite\FortniteClient-Win64-Shipping.exe"""));
        Assert.Equal("Discord.EXE", ProfileRules.NormalizeExe("Discord.EXE"));
        Assert.Equal("", ProfileRules.NormalizeExe("   "));
        Assert.Equal("", ProfileRules.NormalizeExe(null));

        Assert.True(ProfileRules.SameExe("OBS64.exe", "obs64"));
        Assert.True(ProfileRules.SameExe(@"C:\x\Discord.exe", "discord.exe"));
        Assert.False(ProfileRules.SameExe("obs64.exe", "obs32.exe"));
        Assert.False(ProfileRules.SameExe("", ""));
    }

    [Fact]
    public void A_service_entry_matches_its_service_name_or_display_name()
    {
        var s = new ServiceSample("wuauserv", "Windows Update", "", ServiceState.Running, ServiceStartup.Manual, 10, true, true);
        Assert.True(ProfileRules.MatchesService("WUAUSERV", s));
        Assert.True(ProfileRules.MatchesService(" windows update ", s));
        Assert.False(ProfileRules.MatchesService("Windows", s));
        Assert.False(ProfileRules.MatchesService("", s));
    }

    [Fact]
    public void Security_driver_and_peripheral_vendors_are_kept()
    {
        Assert.True(ProfileRules.IsKeptVendor("NVIDIA Corporation"));
        Assert.True(ProfileRules.IsKeptVendor("Advanced Micro Devices, Inc."));
        Assert.True(ProfileRules.IsKeptVendor("Intel Corporation"));
        Assert.True(ProfileRules.IsKeptVendor("Razer Inc."));
        Assert.True(ProfileRules.IsKeptVendor("Malwarebytes"));
        Assert.True(ProfileRules.IsKeptVendor("ESET, spol. s r.o."));
        Assert.False(ProfileRules.IsKeptVendor("Intelligent Systems"));
        Assert.False(ProfileRules.IsKeptVendor("Discord Inc."));
        Assert.False(ProfileRules.IsKeptVendor("Microsoft Corporation"));
        Assert.False(ProfileRules.IsKeptVendor(null));
    }

    [Fact]
    public void The_built_ins_are_gaming_streaming_and_clean_session()
    {
        var profiles = ProfileDefaults.BuiltIns();

        Assert.Equal(new[] { "Gaming Mode", "Streaming / Recording", "Clean Session" }, profiles.Select(p => p.Name));
        Assert.Equal(3, profiles.Select(p => p.Id).Distinct().Count());
        Assert.All(profiles, p => Assert.True(p.IsBuiltIn));
        Assert.All(profiles, p => Assert.False(p.AutoActivate));
        AppProfile streaming = profiles.Single(p => p.Id == "streaming");
        Assert.Equal("obs64.exe", streaming.Trigger);
        Assert.True(streaming.HasTrigger);
        Assert.False(profiles.Single(p => p.Id == "gaming").HasTrigger);
        Assert.Equal(ProfileMode.AllowList, profiles.Single(p => p.Id == "clean").Mode);
    }
}
