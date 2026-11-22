using System;
using System.Collections.Generic;
using System.Linq;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ProfilePlannerTests
{
    private const string WinDir = @"C:\Windows";
    private const int SelfPid = 9999;

    private static ProcessSample P(int pid, string name, string owner = "User", bool window = false, string? path = null) =>
        new(pid, 1000 + pid, name, path ?? @"C:\Program Files\" + name, TimeSpan.Zero, 0, 0, owner, window, false);

    private static ServiceSample S(string name, string display, ServiceState state = ServiceState.Running, bool acceptsStop = true) =>
        new(name, display, "", state, ServiceStartup.Manual, state == ServiceState.Running ? 1 : 0, true, acceptsStop);

    private static AppProfile Profile(ProfileMode mode, string[] apps, string[]? services = null, string trigger = "") =>
        new("p", "Test", "", trigger, false, true, mode, apps, services ?? Array.Empty<string>(), false);

    private static ProfilePlan Plan(AppProfile profile, IReadOnlyList<ProcessSample> processes,
        IReadOnlyList<ServiceSample>? services = null, Func<string?, string?>? companyOf = null) =>
        ProfilePlanner.Plan(profile, processes, services ?? Array.Empty<ServiceSample>(), SelfPid, WinDir, companyOf ?? (_ => null));

    [Fact]
    public void A_close_list_closes_only_the_listed_apps()
    {
        ProfilePlan plan = Plan(Profile(ProfileMode.CloseList, new[] { "OneDrive", "Teams.exe" }), new[]
        {
            P(10, "OneDrive.exe", window: true),
            P(11, "Teams.exe"),
            P(12, "Discord.exe", window: true),
        });

        Assert.Equal(new[] { "OneDrive.exe", "Teams.exe" }, plan.Close.Select(c => c.Name));
        PlannedClose onedrive = plan.Close[0];
        Assert.Equal(10, onedrive.Pid);
        Assert.Equal(1010, onedrive.CreatedTicks);
        Assert.True(onedrive.Graceful);
        Assert.False(plan.Close[1].Graceful);
    }

    [Fact]
    public void An_allowlist_closes_every_other_user_app_except_kept_vendors()
    {
        ProfilePlan plan = Plan(Profile(ProfileMode.AllowList, new[] { "Discord.exe" }), new[]
        {
            P(10, "Discord.exe", window: true),
            P(11, "chrome.exe", window: true),
            P(12, "NVIDIA Overlay.exe", window: true),
            P(13, "Spotify.exe"),
        }, companyOf: path => path is not null && path.Contains("NVIDIA") ? "NVIDIA Corporation" : "Someone");

        Assert.Equal(new[] { "chrome.exe", "Spotify.exe" }, plan.Close.Select(c => c.Name));
    }

    [Fact]
    public void An_allowlist_never_closes_launchers_windows_pieces_anti_cheat_or_audio_tools()
    {
        ProfilePlan plan = Plan(Profile(ProfileMode.AllowList, Array.Empty<string>()), new[]
        {
            P(10, "steam.exe", window: true),
            P(11, "msedgewebview2.exe"),
            P(12, "FortniteClient-Win64-Shipping_EAC_EOS.exe"),
            P(13, "voicemeeter.exe", window: true, path: @"C:\Program Files\VB\Voicemeeter\voicemeeter.exe"),
            P(14, "chrome.exe", window: true),
        }, companyOf: path => path is not null && path.Contains(@"\VB\") ? "VB-Audio Software" : "Someone");

        Assert.Equal(new[] { "chrome.exe" }, plan.Close.Select(c => c.Name));
    }

    [Fact]
    public void Protected_windows_other_users_and_the_game_itself_are_never_touched()
    {
        AppProfile profile = Profile(ProfileMode.AllowList, Array.Empty<string>(), trigger: "Game.exe");
        ProfilePlan plan = Plan(profile, new[]
        {
            P(4, "System"),
            P(SelfPid, "tweaker.exe", window: true),
            P(20, "explorer.exe", window: true, path: @"C:\Windows\explorer.exe"),
            P(21, "EasyAntiCheat.exe"),
            P(22, "SearchHost.exe", path: @"C:\Windows\SystemApps\SearchHost.exe"),
            P(23, "vendor.exe", owner: "SYSTEM"),
            P(24, "GAME.EXE", window: true),
            P(25, "OneDrive.exe"),
        });

        Assert.Equal(new[] { "OneDrive.exe" }, plan.Close.Select(c => c.Name));
    }

    [Fact]
    public void Windowless_helpers_of_a_windowed_app_are_left_to_close_with_it()
    {
        ProfilePlan plan = Plan(Profile(ProfileMode.CloseList, new[] { "chrome.exe" }), new[]
        {
            P(30, "chrome.exe", window: true),
            P(31, "chrome.exe"),
            P(32, "chrome.exe"),
        });

        PlannedClose only = Assert.Single(plan.Close);
        Assert.Equal(30, only.Pid);
        Assert.True(only.Graceful);
    }

    [Fact]
    public void Only_running_stoppable_unprotected_listed_services_are_stopped()
    {
        AppProfile profile = Profile(ProfileMode.CloseList, Array.Empty<string>(),
            new[] { "wuauserv", "Windows Search", "SysMain", "RpcSs", "Vendor" });
        ProfilePlan plan = Plan(profile, Array.Empty<ProcessSample>(), new[]
        {
            S("wuauserv", "Windows Update"),
            S("WSearch", "Windows Search"),
            S("SysMain", "SysMain", ServiceState.Stopped),
            S("RpcSs", "Remote Procedure Call (RPC)"),
            S("Vendor", "Vendor Helper", acceptsStop: false),
            S("Spooler", "Print Spooler"),
        });

        Assert.Equal(new[] { "Windows Search", "Windows Update" }, plan.Stop.Select(s => s.DisplayName));
        Assert.Equal(new[] { "WSearch", "wuauserv" }, plan.Stop.Select(s => s.Name));
        Assert.False(plan.IsEmpty);
        Assert.True(ProfilePlan.Nothing.IsEmpty);
    }
}
