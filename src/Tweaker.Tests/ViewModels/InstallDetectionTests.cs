using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class InstallDetectionTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 14, 30, 0);
    private const int AlreadyInstalled = unchecked((int)0x8A15002B);

    private readonly FakeWingetService _winget = new();
    private readonly FakeInstalledAppDetector _detector = new();
    private readonly FakeInstalledStore _store = new();

    private InstallViewModel Vm() => new(_winget, detector: _detector, store: _store, now: () => Now);

    private static AppRow Row(InstallViewModel vm, string id) => vm.Apps.Single(a => a.WingetId == id);

    [Fact]
    public async Task Detection_marks_detected_apps_and_the_store_dates_them()
    {
        _detector.Next.Add("Discord.Discord");
        _detector.Next.Add("Valve.Steam");
        _store.Record("Valve.Steam", new DateTime(2026, 9, 1, 18, 5, 0));
        InstallViewModel vm = Vm();
        Assert.False(Row(vm, "Discord.Discord").IsInstalled);

        await vm.DetectInstalledAsync();

        Assert.True(Row(vm, "Discord.Discord").IsInstalled);
        Assert.Equal("Already on this PC", Row(vm, "Discord.Discord").InstalledTip);
        Assert.True(Row(vm, "Valve.Steam").IsInstalled);
        Assert.Equal("Installed by Tweaker on 2026-09-01", Row(vm, "Valve.Steam").InstalledTip);
        Assert.False(Row(vm, "Google.Chrome").IsInstalled);
        Assert.False(vm.IsCheckingInstalled);
        Assert.Equal(1, _detector.Calls);
    }

    [Fact]
    public async Task A_stale_store_entry_for_an_uninstalled_app_shows_no_badge()
    {
        _store.Record("Valve.Steam", new DateTime(2026, 9, 1));
        InstallViewModel vm = Vm();

        await vm.DetectInstalledAsync();

        Assert.False(Row(vm, "Valve.Steam").IsInstalled);
        Assert.Equal("", Row(vm, "Valve.Steam").InstalledTip);
    }

    [Fact]
    public async Task A_rebuild_keeps_the_installed_state()
    {
        _detector.Next.Add("Discord.Discord");
        InstallViewModel vm = Vm();
        await vm.DetectInstalledAsync();

        vm.SelectedCategory = "Gaming";
        Assert.True(Row(vm, "Discord.Discord").IsInstalled);
        vm.SelectedCategory = "All";

        Assert.True(Row(vm, "Discord.Discord").IsInstalled);
        Assert.Equal("Already on this PC", Row(vm, "Discord.Discord").InstalledTip);
        Assert.False(Row(vm, "Google.Chrome").IsInstalled);
    }

    [Fact]
    public async Task Without_a_detector_the_store_alone_shows_no_badge()
    {
        _store.Record("Valve.Steam", new DateTime(2026, 9, 1));
        var vm = new InstallViewModel(_winget, store: _store);

        await vm.DetectInstalledAsync();

        Assert.False(Row(vm, "Valve.Steam").IsInstalled);
        Assert.False(Row(vm, "Discord.Discord").IsInstalled);
    }

    [Fact]
    public void A_successful_install_is_recorded_and_marks_the_row()
    {
        InstallViewModel vm = Vm();
        vm.ToggleAppCommand.Execute("Google.Chrome");

        int ok = vm.InstallSelected(new List<string>().Add);

        Assert.Equal(1, ok);
        Assert.Equal(Now, _store.Installed["Google.Chrome"]);
        Assert.True(Row(vm, "Google.Chrome").IsInstalled);
        Assert.Equal("Installed by Tweaker on 2026-09-27", Row(vm, "Google.Chrome").InstalledTip);
    }

    [Fact]
    public void An_already_installed_app_is_marked_but_not_recorded_as_Tweakers()
    {
        _winget.Results["Google.Chrome"] = new InstallResult(true, AlreadyInstalled, "Already installed.");
        InstallViewModel vm = Vm();
        vm.ToggleAppCommand.Execute("Google.Chrome");

        int ok = vm.InstallSelected(new List<string>().Add);

        Assert.Equal(1, ok);
        Assert.False(_store.Installed.ContainsKey("Google.Chrome"));
        Assert.True(Row(vm, "Google.Chrome").IsInstalled);
        Assert.Equal("Already on this PC", Row(vm, "Google.Chrome").InstalledTip);
    }

    [Fact]
    public async Task Detection_runs_once_however_often_the_tab_loads()
    {
        InstallViewModel vm = Vm();

        Task first = vm.EnsureDetectedAsync();
        Task second = vm.EnsureDetectedAsync();
        await first;
        await vm.EnsureDetectedAsync();

        Assert.Same(first, second);
        Assert.Equal(1, _detector.Calls);
    }

    [Fact]
    public async Task A_refresh_while_detection_runs_reuses_it()
    {
        using var gate = new ManualResetEventSlim(false);
        _detector.Gate = gate;
        InstallViewModel vm = Vm();

        Task first = vm.EnsureDetectedAsync();
        Task refresh = vm.DetectInstalledAsync();
        gate.Set();
        await Task.WhenAll(first, refresh);

        Assert.Same(first, refresh);
        Assert.Equal(1, _detector.Calls);

        await vm.DetectInstalledAsync();
        Assert.Equal(2, _detector.Calls);
    }

    [Fact]
    public async Task An_install_that_finishes_during_detection_stays_marked()
    {
        using var gate = new ManualResetEventSlim(false);
        _detector.Gate = gate;
        InstallViewModel vm = Vm();
        Task detect = vm.EnsureDetectedAsync();
        while (_detector.Calls == 0) await Task.Delay(5);

        vm.ToggleAppCommand.Execute("Google.Chrome");
        vm.InstallSelected(new List<string>().Add);
        gate.Set();
        await detect;

        Assert.True(Row(vm, "Google.Chrome").IsInstalled);
        Assert.Equal("Installed by Tweaker on 2026-09-27", Row(vm, "Google.Chrome").InstalledTip);
    }

    [Fact]
    public async Task An_installed_app_shows_ticked_and_cannot_be_selected()
    {
        _detector.Next.Add("Spotify.Spotify");
        InstallViewModel vm = Vm();
        await vm.DetectInstalledAsync();
        AppRow spotify = Row(vm, "Spotify.Spotify");

        Assert.True(spotify.IsTicked);
        spotify.ToggleCommand.Execute(null);
        vm.ToggleAppCommand.Execute("Spotify.Spotify");

        Assert.False(spotify.Selected);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(Row(vm, "Google.Chrome").IsTicked);
    }

    [Fact]
    public async Task A_selected_app_found_installed_leaves_the_selection()
    {
        InstallViewModel vm = Vm();
        vm.ToggleAppCommand.Execute("Discord.Discord");
        Assert.Equal(1, vm.SelectedCount);

        _detector.Next.Add("Discord.Discord");
        await vm.DetectInstalledAsync();

        Assert.Equal(0, vm.SelectedCount);
        Assert.False(Row(vm, "Discord.Discord").Selected);
        Assert.True(Row(vm, "Discord.Discord").IsTicked);
    }
}
