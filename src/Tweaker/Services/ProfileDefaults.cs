using System.Collections.Generic;

namespace Tweaker.Services;

public static class ProfileDefaults
{
    public static IReadOnlyList<AppProfile> BuiltIns() => new[]
    {
        new AppProfile("gaming", "Gaming Mode",
            "Closes background apps and pauses services that compete for CPU and disk.",
            "", false, true, ProfileMode.CloseList,
            new[] { "OneDrive.exe", "Teams.exe", "ms-teams.exe" },
            new[] { "wuauserv", "WSearch", "SysMain" }, true),
        new AppProfile("streaming", "Streaming / Recording",
            "Keeps OBS and chat smooth by closing heavy background apps.",
            "obs64.exe", false, true, ProfileMode.CloseList,
            new[] { "OneDrive.exe", "Teams.exe", "ms-teams.exe", "EpicGamesLauncher.exe" },
            new[] { "wuauserv", "BITS" }, true),
        new AppProfile("clean", "Clean Session",
            "Only essential Windows processes plus what you allow.",
            "", false, true, ProfileMode.AllowList,
            new[] { "Discord.exe" },
            new[] { "wuauserv", "WSearch", "SysMain", "Spooler" }, true),
    };
}
