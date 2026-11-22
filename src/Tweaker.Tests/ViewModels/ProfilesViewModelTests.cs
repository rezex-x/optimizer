using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class ProfilesViewModelTests
{
    private DateTime _clock = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    private readonly FakeProfileStore _store = new();
    private readonly FakeProcessSource _processes = new();
    private readonly FakeServiceSource _services = new();
    private readonly FakeProcessCloser _closer = new();
    private readonly FakeRunningExeSource _running = new();

    private ProfilesViewModel Build(out ProfileManager manager)
    {
        _closer.Source = _processes;
        manager = new ProfileManager(_store, _processes, _services, _closer, _running, 9999, @"C:\Windows", _ => null, () => _clock);
        return new ProfilesViewModel(manager, () => _clock);
    }

    private ProfilesViewModel Build() => Build(out _);

    private static ProcessSample P(int pid, string name, bool window = false) =>
        new(pid, 1000 + pid, name, @"C:\Program Files\" + name, TimeSpan.Zero, 0, 0, "User", window, false);

    private static ServiceSample S(string name, string display) =>
        new(name, display, "", ServiceState.Running, ServiceStartup.Manual, 1, true, true);

    private static AppProfile Fortnite() =>
        new("fn", "Fortnite", "Only Discord stays open.", "FortniteClient-Win64-Shipping.exe", true, true, ProfileMode.AllowList,
            new[] { "Discord.exe" }, new[] { "WSearch" }, false);

    [Fact]
    public void Items_show_their_badges_and_the_first_profile_is_selected()
    {
        _store.Profiles.Add(Fortnite());
        ProfilesViewModel vm = Build();

        Assert.Equal(new[] { "Gaming Mode", "Streaming / Recording", "Clean Session", "Fortnite" }, vm.Items.Select(i => i.Name));
        ProfileItemViewModel gaming = vm.Items[0], fortnite = vm.Items[3];
        Assert.Equal("CLOSE LIST", gaming.ModeText);
        Assert.False(gaming.ShowAuto);
        Assert.False(gaming.ShowCustom);
        Assert.Equal("ALLOWLIST", fortnite.ModeText);
        Assert.False(fortnite.ShowAuto);
        Assert.True(fortnite.ShowCustom);

        Assert.Same(gaming, vm.Selected);
        Assert.True(gaming.IsSelected);
        Assert.Equal("Gaming Mode", vm.Name);
        Assert.Equal(new[] { "OneDrive.exe", "Teams.exe", "ms-teams.exe" }, vm.Apps.Select(a => a.Text));
        Assert.Equal("Apps to close (3)", vm.AppsTitle);
        Assert.Equal("Services to stop (3)", vm.ServicesTitle);
        Assert.False(vm.IsCustom);
        Assert.True(vm.ShowActivate);
        Assert.True(vm.CanActivate);
    }

    [Fact]
    public void The_active_profile_is_selected_and_marked_at_start()
    {
        _store.Session = new ProfileSession("streaming", "Streaming / Recording", _clock, false, false,
            Array.Empty<string>(), Array.Empty<string>(), 0);
        ProfilesViewModel vm = Build();

        Assert.Equal("streaming", vm.Selected!.Id);
        Assert.True(vm.Selected.IsActive);
        Assert.True(vm.IsSelectedActive);
        Assert.True(vm.ShowDeactivate);
        Assert.False(vm.ShowActivate);
    }

    [Fact]
    public void Edits_save_through_the_manager()
    {
        ProfilesViewModel vm = Build();

        vm.Name = "Gaming";
        Assert.Equal("Gaming", _store.Profiles[0].Name);
        Assert.Equal("Gaming", vm.Items[0].Name);
        vm.Name = "   ";
        Assert.Equal("Gaming", vm.Name);

        vm.Description = "Mine";
        Assert.Equal("Mine", _store.Profiles[0].Description);

        Assert.False(vm.CanAutoActivate);
        vm.Trigger = @"C:\Games\game";
        Assert.Equal("game.exe", vm.Trigger);
        Assert.True(vm.CanAutoActivate);
        vm.AutoActivate = true;
        Assert.True(_store.Profiles[0].AutoActivate);
        Assert.True(vm.Items[0].ShowAuto);

        vm.Trigger = "";
        Assert.False(vm.AutoActivate);
        Assert.False(vm.CanAutoActivate);

        vm.RestoreServices = false;
        Assert.False(_store.Profiles[0].RestoreServices);
    }

    [Fact]
    public void The_mode_switch_changes_the_titles_badge_and_note()
    {
        ProfilesViewModel vm = Build();

        vm.SetModeCommand.Execute("AllowList");

        Assert.True(vm.IsAllowList);
        Assert.False(vm.IsCloseList);
        Assert.Equal("Allowed apps (3)", vm.AppsTitle);
        Assert.Equal("ALLOWLIST", vm.Items[0].ModeText);
        Assert.Equal(ProfileMode.AllowList, _store.Profiles[0].Mode);
        Assert.Equal("Every other app you started is closed while this profile is active. Windows, drivers, security software and anti-cheat processes are never touched.",
            vm.AllowNote);

        vm.Selected = vm.Items[1];
        Assert.Equal("Every other app you started is closed while Streaming / Recording runs. Windows, drivers, security software and anti-cheat processes are never touched.",
            vm.AllowNote);
    }

    [Fact]
    public void Apps_are_added_normalized_without_duplicates_and_removed()
    {
        ProfilesViewModel vm = Build();

        vm.NewApp = "spotify";
        vm.AddAppCommand.Execute(null);
        Assert.Contains("spotify.exe", vm.Apps.Select(a => a.Entry));
        Assert.Equal("", vm.NewApp);
        Assert.Contains("spotify.exe", _store.Profiles[0].Apps);

        vm.NewApp = "SPOTIFY.EXE";
        vm.AddAppCommand.Execute(null);
        vm.NewApp = "   ";
        vm.AddAppCommand.Execute(null);
        Assert.Equal(4, vm.Apps.Count);
        Assert.Equal("Apps to close (4)", vm.AppsTitle);

        vm.RemoveAppCommand.Execute(vm.Apps.Single(a => a.Entry == "OneDrive.exe"));
        Assert.DoesNotContain("OneDrive.exe", _store.Profiles[0].Apps);
        Assert.Equal(3, vm.Apps.Count);
    }

    [Fact]
    public async Task Service_entries_show_display_names_once_known()
    {
        _services.Next = new ServiceSnapshot(new[]
        {
            S("wuauserv", "Windows Update"), S("WSearch", "Windows Search"), S("SysMain", "SysMain"), S("Spooler", "Print Spooler"),
        });
        ProfilesViewModel vm = Build();
        Assert.Equal("wuauserv", vm.Services[0].Text);
        Assert.False(vm.Services[0].HasDetail);

        await vm.RefreshAsync();
        Assert.Equal("Windows Update", vm.Services[0].Text);
        Assert.Equal("wuauserv", vm.Services[0].Detail);

        vm.NewService = "print spooler";
        vm.AddServiceCommand.Execute(null);
        Assert.Equal("Spooler", _store.Profiles[0].Services.Last());
        Assert.Equal("Print Spooler", vm.Services.Last().Text);

        vm.NewService = "windows search";
        vm.AddServiceCommand.Execute(null);
        Assert.Equal(4, vm.Services.Count);

        vm.NewService = "MyVendorSvc";
        vm.AddServiceCommand.Execute(null);
        Assert.Equal("MyVendorSvc", vm.Services.Last().Text);

        vm.RemoveServiceCommand.Execute(vm.Services[0]);
        Assert.DoesNotContain("wuauserv", _store.Profiles[0].Services);
    }

    [Fact]
    public void New_profiles_can_be_deleted_with_a_two_step_confirm()
    {
        ProfilesViewModel vm = Build();

        vm.NewCommand.Execute(null);
        Assert.Equal(4, vm.Items.Count);
        Assert.Equal("New profile", vm.Name);
        Assert.True(vm.IsCustom);
        Assert.True(vm.ShowDelete);

        vm.DeleteCommand.Execute(null);
        Assert.True(vm.ShowConfirmDelete);
        Assert.False(vm.ShowDelete);
        Assert.Equal("Delete New profile? Click Confirm to delete it.", vm.Message);

        _clock = _clock.AddMilliseconds(200);
        vm.ConfirmDeleteCommand.Execute(null);
        Assert.Equal(4, vm.Items.Count);

        _clock = _clock.AddSeconds(1);
        vm.ConfirmDeleteCommand.Execute(null);
        Assert.Equal(3, vm.Items.Count);
        Assert.Same(vm.Items[0], vm.Selected);
        Assert.Equal("Deleted New profile", vm.Message);
        Assert.Equal(3, _store.Profiles.Count);
    }

    [Fact]
    public async Task Activate_previews_first_and_needs_a_separate_confirm()
    {
        _processes.Next = new ProcessSnapshot(TimeSpan.FromSeconds(1),
            new[] { P(10, "OneDrive.exe", window: true), P(11, "Teams.exe"), P(12, "Teams.exe") }, 0, 1);
        _services.Next = new ServiceSnapshot(new[] { S("wuauserv", "Windows Update") });
        ProfilesViewModel vm = Build(out ProfileManager manager);

        await vm.ActivateAsync();

        Assert.True(vm.IsPreviewing);
        Assert.False(vm.ShowActivate);
        Assert.False(vm.IsPreviewEmpty);
        Assert.Equal("Activating Gaming Mode will:", vm.PreviewTitle);
        Assert.Equal("OneDrive.exe, Teams.exe ×2", vm.PreviewCloseText);
        Assert.Equal("Windows Update", vm.PreviewStopText);

        _clock = _clock.AddMilliseconds(200);
        await vm.ConfirmActivateAsync();
        Assert.False(manager.IsActive);

        _clock = _clock.AddSeconds(1);
        await vm.ConfirmActivateAsync();

        Assert.True(manager.IsActive);
        Assert.False(vm.IsPreviewing);
        Assert.Equal("Gaming Mode active — closed 3 apps, stopped 1 service.", vm.Message);
        Assert.True(vm.Items[0].IsActive);
        Assert.True(vm.ShowDeactivate);
    }

    [Fact]
    public async Task An_empty_preview_says_so_and_cancel_closes_it()
    {
        ProfilesViewModel vm = Build();

        await vm.ActivateAsync();
        Assert.True(vm.IsPreviewEmpty);

        vm.CancelPreviewCommand.Execute(null);
        Assert.False(vm.IsPreviewing);
        Assert.True(vm.ShowActivate);
    }

    [Fact]
    public async Task Editing_closes_an_open_preview()
    {
        ProfilesViewModel vm = Build();
        await vm.ActivateAsync();
        Assert.True(vm.IsPreviewing);

        vm.RemoveAppCommand.Execute(vm.Apps[0]);

        Assert.False(vm.IsPreviewing);
        _clock = _clock.AddSeconds(1);
        await vm.ConfirmActivateAsync();
        Assert.False(vm.Manager.IsActive);
    }

    [Fact]
    public async Task Deactivate_restores_services_and_reports()
    {
        _services.Next = new ServiceSnapshot(new[] { S("wuauserv", "Windows Update") });
        ProfilesViewModel vm = Build(out ProfileManager manager);
        await vm.ActivateAsync();
        _clock = _clock.AddSeconds(1);
        await vm.ConfirmActivateAsync();

        await vm.DeactivateAsync();

        Assert.False(manager.IsActive);
        Assert.Equal("Gaming Mode deactivated — restarted 1 service.", vm.Message);
        Assert.False(vm.IsSelectedActive);
        Assert.False(vm.Items[0].IsActive);
    }

    [Fact]
    public async Task Another_active_profile_blocks_activation()
    {
        _store.Session = new ProfileSession("streaming", "Streaming / Recording", _clock, false, false,
            Array.Empty<string>(), Array.Empty<string>(), 0);
        ProfilesViewModel vm = Build();
        vm.Selected = vm.Items[0];

        Assert.False(vm.CanActivate);
        await vm.ActivateAsync();

        Assert.False(vm.IsPreviewing);
        Assert.Equal("Streaming / Recording is already active — deactivate it first.", vm.Message);
    }

    [Fact]
    public async Task Changes_made_by_the_watcher_show_up()
    {
        _store.Profiles = new List<AppProfile> { Fortnite() with { Mode = ProfileMode.CloseList } };
        ProfilesViewModel vm = Build(out ProfileManager manager);
        _running.Next = FakeRunningExeSource.Names("FortniteClient-Win64-Shipping.exe");

        await manager.TickAsync();

        Assert.True(vm.Items[0].IsActive);
        Assert.True(vm.IsSelectedActive);
        Assert.StartsWith("Fortnite active — ", vm.Message);
    }

    [Fact]
    public void Switching_to_an_allowlist_turns_automatic_activation_off()
    {
        _store.Profiles.Add(Fortnite() with { Mode = ProfileMode.CloseList });
        ProfilesViewModel vm = Build();
        vm.Selected = vm.Items[3];
        Assert.True(vm.AutoActivate);
        Assert.True(vm.CanAutoActivate);

        vm.SetModeCommand.Execute("AllowList");

        Assert.False(vm.AutoActivate);
        Assert.False(vm.CanAutoActivate);
        Assert.False(_store.Profiles[3].AutoActivate);
        Assert.False(vm.Items[3].ShowAuto);

        vm.AutoActivate = true;
        Assert.False(_store.Profiles[3].AutoActivate);

        vm.SetModeCommand.Execute("CloseList");
        Assert.True(vm.CanAutoActivate);
        Assert.False(vm.AutoActivate);
    }

    [Fact]
    public async Task A_session_whose_profile_is_gone_can_still_be_deactivated()
    {
        _store.Session = new ProfileSession("gone", "Old Game", _clock, false, false,
            new[] { "SysMain" }, new[] { "SysMain" }, 0);
        ProfilesViewModel vm = Build(out ProfileManager manager);

        Assert.True(vm.IsOrphanActive);
        Assert.True(vm.ShowDeactivate);
        Assert.Equal("Old Game is still active but its profile no longer exists — Deactivate restores its services.", vm.Message);

        await vm.DeactivateAsync();

        Assert.Equal(new[] { "SysMain" }, _services.Started);
        Assert.False(manager.IsActive);
        Assert.False(vm.IsOrphanActive);
        Assert.False(vm.ShowDeactivate);
        Assert.Equal("Old Game deactivated — restarted 1 service.", vm.Message);
    }

    [Fact]
    public async Task A_preview_expires_after_a_minute()
    {
        ProfilesViewModel vm = Build(out ProfileManager manager);
        await vm.ActivateAsync();
        Assert.True(vm.IsPreviewing);

        _clock = _clock.AddSeconds(61);
        await vm.ConfirmActivateAsync();

        Assert.False(manager.IsActive);
        Assert.False(vm.IsPreviewing);
        Assert.True(vm.ShowActivate);
        Assert.Equal("Preview expired — click Activate now again.", vm.Message);
    }
}
