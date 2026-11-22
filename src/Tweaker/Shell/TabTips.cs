using Tweaker.ViewModels;

namespace Tweaker.Shell;

public static class TabTips
{
    public static string For(Tab tab, ResourceTab? sub) => tab switch
    {
        Tab.Tweaks => "Switches queue changes in the bar below; Apply runs them all. Restart-badged tweaks take effect after a restart.",
        Tab.Config => "Config switches queue like Tweaks; applied rows are tinted green.",
        Tab.Nvidia => "Settings change the global driver profile; Revert puts back the value from before Tweaker changed it.",
        Tab.Install => "Apps install through winget; installed apps are detected and marked.",
        Tab.Updates => "Pausing or deferring updates is safer than turning Windows Update off.",
        Tab.Hardware => "Power-saving switches apply at once; interrupt changes need a restart.",
        Tab.Inspector => "Saving keeps a .bak next to the file; close the game before editing its config.",
        Tab.Resources => ForResources(sub ?? ResourceTab.Profiles),
        _ => "",
    };

    private static string ForResources(ResourceTab sub) => sub switch
    {
        ResourceTab.Processes =>
            "CPU is a share of all logical processors; RAM is the working set (Task Manager's Details shows private memory, so it reads lower); " +
            "Disk counts file reads and writes. GPU % is the busiest GPU engine a process uses and shows — without a WDDM 2 GPU. " +
            "Apps are programs with their own window; File Explorer, Task Manager and Store-app frames live in the Windows folder, so they list under Windows.",
        ResourceTab.Services =>
            "Stop never stops the services that depend on it. Protected services keep Windows, security software or an anti-cheat running; " +
            "\"can't stop\" means Windows itself refuses. Stopped services start again on reboot if their startup is Automatic.",
        ResourceTab.Performance => "Usage refreshes every second; temperatures need the PawnIO driver.",
        ResourceTab.Network => "Ping runs only while this tab is open.",
        _ => "A profile closes the listed apps and stops services while its game runs, then restores them.",
    };
}
