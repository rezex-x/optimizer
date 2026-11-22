using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class ProfileManagerTests
{
    private sealed class Rig
    {
        public FakeProfileStore Store { get; } = new();
        public FakeProcessSource Processes { get; } = new();
        public FakeServiceSource Services { get; } = new();
        public FakeProcessCloser Closer { get; } = new();
        public FakeRunningExeSource Running { get; } = new();
        public TimeSpan? CloseWait { get; set; }

        public ProfileManager Build(IServiceSource? services = null) => new(Store, Processes, services ?? Services, Closer, Running,
            9999, @"C:\Windows", _ => null, () => new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc), CloseWait);
    }

    private sealed class RecordingServiceSource : IServiceSource
    {
        private readonly FakeServiceSource _inner;
        private readonly FakeProfileStore _store;
        private int _stops;

        public RecordingServiceSource(FakeServiceSource inner, FakeProfileStore store)
        {
            _inner = inner;
            _store = store;
        }

        public IReadOnlyList<string>? StoredAtSecondStop { get; private set; }

        public ServiceSnapshot Read() => _inner.Read();
        public string? Start(string name) => _inner.Start(name);

        public string? Stop(string name)
        {
            if (++_stops == 2) StoredAtSecondStop = _store.Session?.StoppedServices.ToList();
            return _inner.Stop(name);
        }
    }

    private static ProcessSample P(int pid, string name, bool window = false) =>
        new(pid, 1000 + pid, name, @"C:\Program Files\" + name, TimeSpan.Zero, 0, 0, "User", window, false);

    private static ServiceSample S(string name, string display) =>
        new(name, display, "", ServiceState.Running, ServiceStartup.Manual, 1, true, true);

    private static AppProfile Game(bool auto = true, bool restore = true) =>
        new("game", "Game", "", "Game.exe", auto, restore, ProfileMode.CloseList,
            new[] { "OneDrive.exe", "Teams.exe" }, new[] { "wuauserv" }, false);

    private static Rig GameRig(bool auto = true, bool restore = true)
    {
        var rig = new Rig();
        rig.Store.Profiles = new List<AppProfile> { Game(auto, restore) };
        rig.Processes.Next = new ProcessSnapshot(TimeSpan.FromSeconds(1),
            new[] { P(10, "OneDrive.exe", window: true), P(11, "Teams.exe"), P(12, "Discord.exe", window: true) }, 0, 1);
        rig.Services.Next = new ServiceSnapshot(new[] { S("wuauserv", "Windows Update"), S("WSearch", "Windows Search") });
        rig.Closer.Source = rig.Processes;
        return rig;
    }

    [Fact]
    public void Profiles_load_from_the_store_and_edits_save_through_it()
    {
        var rig = new Rig();
        ProfileManager m = rig.Build();
        Assert.Equal(3, m.Profiles.Count);

        AppProfile created = m.Create();
        Assert.Equal("New profile", created.Name);
        Assert.False(created.IsBuiltIn);
        Assert.Equal("New profile 2", m.Create().Name);
        Assert.Equal(5, rig.Store.Profiles.Count);

        m.Save(created with { Name = "Fortnite" });
        Assert.Equal("Fortnite", rig.Store.Profiles.Single(p => p.Id == created.Id).Name);

        Assert.False(m.Delete("gaming"));
        Assert.True(m.Delete(created.Id));
        Assert.Equal(4, rig.Store.Profiles.Count);
        Assert.Null(m.Find(created.Id));
    }

    [Fact]
    public async Task Activating_closes_apps_stops_services_and_saves_the_session()
    {
        Rig rig = GameRig();
        ProfileManager m = rig.Build();
        AppProfile game = m.Find("game")!;

        ProfilePlan plan = await m.PreviewAsync(game);
        Assert.Equal(new[] { "OneDrive.exe", "Teams.exe" }, plan.Close.Select(c => c.Name));
        Assert.Equal(new[] { "wuauserv" }, plan.Stop.Select(s => s.Name));

        string message = await m.ActivateAsync(game, plan, automatic: false);

        Assert.Equal("Game active — closed 2 apps, stopped 1 service.", message);
        Assert.Equal(new[] { (10, 1010L) }, rig.Closer.Closed);
        Assert.Equal(new[] { (11, 1011L) }, rig.Processes.Killed);
        Assert.Equal(new[] { "wuauserv" }, rig.Services.Stopped);
        Assert.True(m.IsActive);
        Assert.Equal("game", m.ActiveId);
        Assert.Equal("GAME PROFILE ACTIVE — 2 APPS CLOSED", m.BadgeText);
        ProfileSession saved = rig.Store.Session!;
        Assert.Equal(new[] { "wuauserv" }, saved.StoppedServices);
        Assert.Equal(new[] { "wuauserv" }, saved.ManagedServices);
        Assert.False(saved.Automatic);
        Assert.False(saved.TriggerSeen);
        Assert.True(m.Manages("WUAUSERV", "Windows Update"));
        Assert.False(m.Manages("WSearch", "Windows Search"));
    }

    [Fact]
    public async Task Failures_are_reported_and_only_stopped_services_are_recorded()
    {
        Rig rig = GameRig();
        rig.Closer.Result = "it has no window to close";
        rig.Services.StopResult = "Access is denied.";
        ProfileManager m = rig.Build();
        AppProfile game = m.Find("game")!;

        string message = await m.ActivateAsync(game, await m.PreviewAsync(game), automatic: false);

        Assert.Equal("Game active — closed 1 app, stopped 0 services. 1 app didn't close, 1 service didn't stop.", message);
        Assert.Empty(rig.Store.Session!.StoppedServices);
        Assert.Equal("GAME PROFILE ACTIVE — 1 APP CLOSED", m.BadgeText);
    }

    [Fact]
    public async Task Only_one_profile_is_active_at_a_time()
    {
        Rig rig = GameRig();
        ProfileManager m = rig.Build();
        AppProfile game = m.Find("game")!;
        await m.ActivateAsync(game, ProfilePlan.Nothing, automatic: false);

        Assert.Equal("Game is already active — deactivate it first.", await m.ActivateAsync(game, ProfilePlan.Nothing, false));
        Assert.False(m.Delete("game"));
    }

    [Fact]
    public async Task Deactivating_restarts_only_the_services_it_stopped()
    {
        Rig rig = GameRig();
        ProfileManager m = rig.Build();
        AppProfile game = m.Find("game")!;
        Assert.Equal("No profile is active.", await m.DeactivateAsync(restore: true));

        await m.ActivateAsync(game, await m.PreviewAsync(game), automatic: false);
        string message = await m.DeactivateAsync(restore: true);

        Assert.Equal("Game deactivated — restarted 1 service.", message);
        Assert.Equal(new[] { "wuauserv" }, rig.Services.Started);
        Assert.False(m.IsActive);
        Assert.Equal("", m.BadgeText);
        Assert.Null(rig.Store.Session);

        await m.ActivateAsync(game, await m.PreviewAsync(game), automatic: false);
        Assert.Equal("Game deactivated.", await m.DeactivateAsync(restore: false));
        Assert.Single(rig.Services.Started);
    }

    [Fact]
    public async Task A_session_left_by_the_last_run_is_still_active_and_restorable()
    {
        var rig = new Rig();
        rig.Store.Session = new ProfileSession("gaming", "Gaming Mode", DateTime.UtcNow, false, false,
            new[] { "SysMain" }, new[] { "wuauserv", "WSearch", "SysMain" }, 1);
        ProfileManager m = rig.Build();

        Assert.True(m.IsActive);
        Assert.Equal("GAMING MODE PROFILE ACTIVE — 1 APP CLOSED", m.BadgeText);
        Assert.True(m.Manages("SysMain", "SysMain"));

        await m.DeactivateAsync(restore: true);
        Assert.Equal(new[] { "SysMain" }, rig.Services.Started);
    }

    [Fact]
    public async Task An_auto_profile_activates_when_its_game_starts()
    {
        Rig rig = GameRig(auto: true);
        ProfileManager m = rig.Build();

        rig.Running.Next = FakeRunningExeSource.Names("explorer.exe");
        await m.TickAsync();
        Assert.False(m.IsActive);

        rig.Running.Next = FakeRunningExeSource.Names("explorer.exe", "GAME.EXE");
        await m.TickAsync();

        Assert.True(m.IsActive);
        Assert.True(m.Session!.Automatic);
        Assert.True(m.Session.TriggerSeen);
        Assert.Equal("Game active — closed 2 apps, stopped 1 service.", m.LastMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_game_exiting_deactivates_and_restarts_services_only_when_asked(bool restore)
    {
        Rig rig = GameRig(auto: true, restore: restore);
        ProfileManager m = rig.Build();
        rig.Running.Next = FakeRunningExeSource.Names("Game.exe");
        await m.TickAsync();
        Assert.True(m.IsActive);

        rig.Running.Next = FakeRunningExeSource.Names("explorer.exe");
        await m.TickAsync();

        Assert.False(m.IsActive);
        Assert.Equal(restore ? new[] { "wuauserv" } : Array.Empty<string>(), rig.Services.Started);
        Assert.Equal(restore ? "Game deactivated — restarted 1 service." : "Game deactivated.", m.LastMessage);
    }

    [Fact]
    public async Task A_manual_activation_waits_to_see_the_game_before_its_exit_counts()
    {
        Rig rig = GameRig(auto: false);
        ProfileManager m = rig.Build();
        await m.ActivateAsync(m.Find("game")!, ProfilePlan.Nothing, automatic: false);

        rig.Running.Next = FakeRunningExeSource.Names("explorer.exe");
        await m.TickAsync();
        Assert.True(m.IsActive);

        rig.Running.Next = FakeRunningExeSource.Names("Game.exe");
        await m.TickAsync();
        Assert.True(m.Session!.TriggerSeen);
        Assert.True(rig.Store.Session!.TriggerSeen);

        rig.Running.Next = FakeRunningExeSource.Names("explorer.exe");
        await m.TickAsync();
        Assert.False(m.IsActive);
    }

    [Fact]
    public async Task Deactivating_by_hand_while_the_game_runs_stops_it_re_activating()
    {
        Rig rig = GameRig(auto: true);
        ProfileManager m = rig.Build();
        rig.Running.Next = FakeRunningExeSource.Names("Game.exe");
        await m.TickAsync();

        Assert.Equal("Game deactivated — restarted 1 service.", await m.DeactivateByHandAsync());
        await m.TickAsync();
        Assert.False(m.IsActive);

        rig.Running.Next = FakeRunningExeSource.Names("explorer.exe");
        await m.TickAsync();
        rig.Running.Next = FakeRunningExeSource.Names("Game.exe");
        await m.TickAsync();
        Assert.True(m.IsActive);
    }

    [Fact]
    public async Task A_failed_running_read_changes_nothing()
    {
        Rig rig = GameRig(auto: true);
        ProfileManager m = rig.Build();
        rig.Running.Next = FakeRunningExeSource.Names("Game.exe");
        await m.TickAsync();

        rig.Running.Next = null;
        await m.TickAsync();

        Assert.True(m.IsActive);
    }

    [Fact]
    public async Task Nothing_is_watched_without_an_auto_profile_or_a_session()
    {
        var rig = new Rig();
        ProfileManager m = rig.Build();

        await m.TickAsync();

        Assert.Equal(0, rig.Running.Reads);
    }

    [Fact]
    public async Task An_app_that_hides_to_the_tray_is_ended_with_its_helpers_and_counts_as_closed()
    {
        Rig rig = GameRig();
        rig.CloseWait = TimeSpan.Zero;
        rig.Closer.Effect = FakeCloseEffect.GoesToTray;
        rig.Processes.Next = new ProcessSnapshot(TimeSpan.FromSeconds(1),
            new[] { P(10, "OneDrive.exe", window: true), P(20, "OneDrive.exe"), P(11, "Teams.exe") }, 0, 1);
        ProfileManager m = rig.Build();
        AppProfile game = m.Find("game")!;

        string message = await m.ActivateAsync(game, await m.PreviewAsync(game), automatic: false);

        Assert.Equal("Game active — closed 2 apps, stopped 1 service.", message);
        Assert.Equal(new[] { (10, 1010L) }, rig.Closer.Closed);
        Assert.Equal(new[] { (11, 1011L), (10, 1010L), (20, 1020L) }, rig.Processes.Killed);
        Assert.Equal(2, m.Session!.ClosedApps);
    }

    [Fact]
    public async Task An_app_still_showing_its_window_is_asking_to_save_and_not_counted()
    {
        Rig rig = GameRig();
        rig.CloseWait = TimeSpan.Zero;
        rig.Closer.Effect = FakeCloseEffect.StaysOpen;
        ProfileManager m = rig.Build();
        AppProfile game = m.Find("game")!;

        string message = await m.ActivateAsync(game, await m.PreviewAsync(game), automatic: false);

        Assert.Equal("Game active — closed 1 app, stopped 1 service. 1 app is asking to save.", message);
        Assert.Equal(new[] { (11, 1011L) }, rig.Processes.Killed);
        Assert.Equal("GAME PROFILE ACTIVE — 1 APP CLOSED", m.BadgeText);
    }

    [Fact]
    public async Task The_restore_record_is_saved_after_every_service_stop()
    {
        Rig rig = GameRig();
        rig.Store.Profiles = new List<AppProfile> { Game() with { Services = new[] { "wuauserv", "WSearch" } } };
        var recording = new RecordingServiceSource(rig.Services, rig.Store);
        ProfileManager m = rig.Build(recording);
        AppProfile game = m.Find("game")!;

        await m.ActivateAsync(game, await m.PreviewAsync(game), automatic: false);

        Assert.Equal(new[] { "WSearch", "wuauserv" }, rig.Services.Stopped);
        Assert.NotNull(recording.StoredAtSecondStop);
        Assert.Contains("WSearch", recording.StoredAtSecondStop!);
        Assert.Equal(new[] { "WSearch", "wuauserv" }, rig.Store.Session!.StoppedServices);
    }

    [Fact]
    public async Task An_allowlist_profile_never_activates_on_its_own()
    {
        var rig = new Rig();
        rig.Store.Profiles = new List<AppProfile> { Game(auto: true) with { Mode = ProfileMode.AllowList } };
        ProfileManager m = rig.Build();
        rig.Running.Next = FakeRunningExeSource.Names("Game.exe");

        await m.TickAsync();

        Assert.False(m.IsActive);
        Assert.Empty(rig.Closer.Closed);
        Assert.Empty(rig.Processes.Killed);
    }

    [Fact]
    public async Task Deactivating_by_hand_suppresses_every_profile_for_that_game()
    {
        Rig rig = GameRig(auto: true);
        rig.Store.Profiles.Add(Game(auto: true) with { Id = "game2", Name = "Game 2", Trigger = "game" });
        ProfileManager m = rig.Build();
        rig.Running.Next = FakeRunningExeSource.Names("Game.exe");
        await m.TickAsync();
        Assert.Equal("game", m.ActiveId);

        await m.DeactivateByHandAsync();
        await m.TickAsync();

        Assert.False(m.IsActive);
    }

    [Fact]
    public async Task A_failed_restart_names_the_service_and_the_reason()
    {
        Rig rig = GameRig();
        rig.Services.StartResult = "Access is denied.";
        ProfileManager m = rig.Build();
        AppProfile game = m.Find("game")!;
        await m.ActivateAsync(game, await m.PreviewAsync(game), automatic: false);

        string message = await m.DeactivateAsync(restore: true);

        Assert.Equal("Game deactivated — restarted 0 services. wuauserv didn't restart (Access is denied.).", message);
    }

    [Fact]
    public async Task Renaming_the_active_profile_renames_its_session()
    {
        Rig rig = GameRig();
        ProfileManager m = rig.Build();
        AppProfile game = m.Find("game")!;
        await m.ActivateAsync(game, await m.PreviewAsync(game), automatic: false);

        m.Save(m.Find("game")! with { Name = "Apex" });

        Assert.Equal("APEX PROFILE ACTIVE — 2 APPS CLOSED", m.BadgeText);
        Assert.Equal("Apex", rig.Store.Session!.ProfileName);
    }

    [Fact]
    public async Task Service_names_map_to_display_names()
    {
        Rig rig = GameRig();
        ProfileManager m = rig.Build();

        IReadOnlyDictionary<string, string> names = await m.ServiceNamesAsync();

        Assert.Equal("Windows Update", names["WUAUSERV"]);
        Assert.Equal(2, names.Count);
    }
}
