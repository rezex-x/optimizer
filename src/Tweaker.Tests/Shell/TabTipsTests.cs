using System;
using Tweaker.Shell;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.Shell;

public class TabTipsTests
{
    [Fact]
    public void Every_tab_and_resources_sub_tab_has_a_tip()
    {
        foreach (Tab tab in Enum.GetValues<Tab>())
            Assert.False(string.IsNullOrWhiteSpace(TabTips.For(tab, null)), tab.ToString());
        foreach (ResourceTab sub in Enum.GetValues<ResourceTab>())
            Assert.False(string.IsNullOrWhiteSpace(TabTips.For(Tab.Resources, sub)), sub.ToString());
    }

    [Fact]
    public void The_tweaks_tip_is_exact()
    {
        Assert.Equal("Switches queue changes in the bar below; Apply runs them all. Restart-badged tweaks take effect after a restart.",
            TabTips.For(Tab.Tweaks, null));
    }

    [Fact]
    public void The_services_tip_is_the_old_bottom_note()
    {
        Assert.StartsWith("Stop never stops the services that depend on it.", TabTips.For(Tab.Resources, ResourceTab.Services));
    }

    [Fact]
    public void The_processes_tip_is_the_old_bottom_note()
    {
        Assert.StartsWith("CPU is a share of all logical processors", TabTips.For(Tab.Resources, ResourceTab.Processes));
    }

    [Fact]
    public void Resources_without_a_sub_tab_shows_the_app_profiles_tip()
    {
        Assert.Equal(TabTips.For(Tab.Resources, ResourceTab.Profiles), TabTips.For(Tab.Resources, null));
    }
}
