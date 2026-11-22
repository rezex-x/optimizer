using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class ProcessesViewModelTests
{
    private const string WinDir = @"C:\Windows";
    private const int Self = 9999;

    private static ProcessSample P(int pid, string name, double cpuSeconds = 0, long ramMb = 100, long io = 0,
        string? path = @"C:\Apps\x.exe", bool window = false, string owner = "User", bool critical = false, long created = 1) =>
        new(pid, created, name, path, TimeSpan.FromSeconds(cpuSeconds), ramMb * 1024 * 1024, io, owner, window, critical);

    private static ProcessSnapshot At(double seconds, params ProcessSample[] processes) =>
        new(TimeSpan.FromSeconds(seconds), processes, 32L << 30, 4);

    private static (ProcessesViewModel vm, FakeProcessSource source, FakeGpuUsageSource gpu) Build(Func<DateTime>? now = null)
    {
        var source = new FakeProcessSource();
        var gpu = new FakeGpuUsageSource();
        return (new ProcessesViewModel(source, gpu, Self, WinDir, now), source, gpu);
    }

    [Fact]
    public async Task Ending_a_process_records_its_memory_once()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var source = new FakeProcessSource();
        var freed = new FreedResourcesViewModel();
        var vm = new ProcessesViewModel(source, new FakeGpuUsageSource(), Self, WinDir, () => clock, freed: freed);
        source.Next = At(1, P(4820, "chrome.exe", ramMb: 300, created: 77));
        await vm.RefreshAsync();
        ProcessRowViewModel chrome = vm.Rows.Single();

        vm.EndTaskCommand.Execute(chrome);
        clock = clock.AddSeconds(1);
        await vm.ConfirmEndTaskAsync(chrome);

        Assert.Equal(300, freed.MeasuredMb, 3);

        vm.EndTaskCommand.Execute(chrome);
        clock = clock.AddSeconds(1);
        await vm.ConfirmEndTaskAsync(chrome);
        Assert.Equal(300, freed.MeasuredMb, 3);
    }

    [Fact]
    public async Task A_failed_end_task_records_nothing()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var source = new FakeProcessSource { KillResult = "Access is denied." };
        var freed = new FreedResourcesViewModel();
        var vm = new ProcessesViewModel(source, new FakeGpuUsageSource(), Self, WinDir, () => clock, freed: freed);
        source.Next = At(1, P(4820, "chrome.exe", ramMb: 300));
        await vm.RefreshAsync();
        ProcessRowViewModel chrome = vm.Rows.Single();

        vm.EndTaskCommand.Execute(chrome);
        clock = clock.AddSeconds(1);
        await vm.ConfirmEndTaskAsync(chrome);

        Assert.Equal(0, freed.MeasuredMb);
    }

    [Fact]
    public async Task Rates_read_dashes_until_a_second_sample_then_show_cpu_disk_and_gpu()
    {
        var (vm, source, gpu) = Build();
        source.Next = At(1, P(100, "game.exe", cpuSeconds: 10), P(200, "idle.exe"));
        await vm.RefreshAsync();
        ProcessRowViewModel game = vm.Rows.Single(r => r.Pid == 100);
        Assert.Equal("—", game.CpuText);
        Assert.Equal("—", game.DiskText);
        Assert.Equal("—", game.GpuText);

        source.Next = At(2.5, P(100, "game.exe", cpuSeconds: 13, io: 3L * 1024 * 1024), P(200, "idle.exe"));
        gpu.Next = new Dictionary<int, double> { [100] = 71.4 };
        await vm.RefreshAsync();

        Assert.Same(game, vm.Rows.Single(r => r.Pid == 100));
        Assert.Equal("50.0", game.CpuText);
        Assert.Equal(UsageLevel.Hot, game.CpuLevel);
        Assert.Equal("2.0", game.DiskText);
        Assert.Equal("71.4", game.GpuText);
        Assert.Equal(UsageLevel.Hot, game.GpuLevel);
        Assert.Equal("100", game.RamText);
        Assert.Equal("0.0", vm.Rows.Single(r => r.Pid == 200).GpuText);
    }

    [Fact]
    public async Task A_long_gap_starts_the_rates_fresh()
    {
        var (vm, source, _) = Build();
        source.Next = At(1, P(100, "game.exe", cpuSeconds: 10));
        await vm.RefreshAsync();
        source.Next = At(60, P(100, "game.exe", cpuSeconds: 70));
        await vm.RefreshAsync();
        Assert.Equal("—", vm.Rows.Single().CpuText);
    }

    [Fact]
    public async Task The_default_sort_is_cpu_highest_first_and_headers_change_it()
    {
        var (vm, source, _) = Build();
        source.Next = At(1, P(1, "a.exe"), P(2, "b.exe"), P(3, "c.exe"));
        await vm.RefreshAsync();
        source.Next = At(2, P(1, "a.exe", 0.1), P(2, "b.exe", 0.5), P(3, "c.exe", 0.3));
        await vm.RefreshAsync();
        Assert.Equal(new[] { "b.exe", "c.exe", "a.exe" }, vm.Rows.Select(r => r.Name));

        vm.SortBy("Name");
        Assert.Equal("Name", vm.SortColumn);
        Assert.True(vm.SortAscending);
        Assert.Equal(new[] { "a.exe", "b.exe", "c.exe" }, vm.Rows.Select(r => r.Name));

        vm.SortBy("Name");
        Assert.Equal(new[] { "c.exe", "b.exe", "a.exe" }, vm.Rows.Select(r => r.Name));

        vm.SortBy("Pid");
        Assert.False(vm.SortAscending);
        Assert.Equal(new[] { 3, 2, 1 }, vm.Rows.Select(r => r.Pid));
    }

    [Fact]
    public async Task The_view_filter_splits_apps_background_and_windows_without_a_new_read()
    {
        var (vm, source, _) = Build();
        source.Next = At(1,
            P(4, "System", path: null, owner: "SYSTEM"),
            P(1024, "explorer.exe", path: @"C:\Windows\explorer.exe", window: true),
            P(4820, "chrome.exe", path: @"C:\Program Files\Google\Chrome\chrome.exe", window: true),
            P(2044, "OneDrive.exe", path: @"C:\Users\player\OneDrive.exe"));
        await vm.RefreshAsync();

        vm.SetViewCommand.Execute("Apps");
        Assert.True(vm.IsApps);
        Assert.Equal(new[] { "chrome.exe" }, vm.Rows.Select(r => r.Name));
        Assert.Equal("1 process", vm.CountText);

        vm.SetViewCommand.Execute("Background");
        Assert.Equal(new[] { "OneDrive.exe" }, vm.Rows.Select(r => r.Name));

        vm.SetViewCommand.Execute("Windows");
        Assert.Equal(2, vm.Rows.Count);

        vm.SetViewCommand.Execute("All");
        Assert.True(vm.IsAll);
        Assert.Equal("4 processes", vm.CountText);
        Assert.Equal(1, source.Reads);
    }

    [Fact]
    public async Task The_text_filter_matches_names_or_an_exact_pid()
    {
        var (vm, source, _) = Build();
        source.Next = At(1, P(4820, "chrome.exe"), P(6723, "Discord.exe"));
        await vm.RefreshAsync();

        vm.Filter = "DISC";
        Assert.Equal(new[] { "Discord.exe" }, vm.Rows.Select(r => r.Name));

        vm.Filter = "4820";
        Assert.Equal(new[] { "chrome.exe" }, vm.Rows.Select(r => r.Name));

        vm.Filter = "";
        Assert.Equal(2, vm.Rows.Count);
    }

    [Fact]
    public async Task Ram_switches_between_megabytes_and_percent_of_installed_memory()
    {
        var (vm, source, _) = Build();
        source.Next = At(1, P(100, "game.exe", ramMb: 3277));
        await vm.RefreshAsync();
        ProcessRowViewModel row = vm.Rows.Single();
        Assert.Equal("3277", row.RamText);
        Assert.Equal(UsageLevel.Hot, row.RamLevel);

        vm.SetRamUnitCommand.Execute("%");
        Assert.True(vm.IsRamPercent);
        Assert.Equal("10.0", row.RamText);

        vm.SetRamUnitCommand.Execute("MB");
        Assert.True(vm.IsRamMb);
        Assert.Equal("3277", row.RamText);
    }

    [Fact]
    public async Task Protected_rows_never_reach_the_kill_call()
    {
        var (vm, source, _) = Build();
        source.Next = At(1,
            P(700, "csrss.exe", path: @"C:\Windows\System32\csrss.exe", owner: "SYSTEM"),
            P(Self, "tweaker.exe"),
            P(5000, "vgc.exe"));
        await vm.RefreshAsync();
        Assert.All(vm.Rows, r => Assert.True(r.IsProtected));
        Assert.All(vm.Rows, r => Assert.False(r.ShowEndTask));

        ProcessRowViewModel csrss = vm.Rows.Single(r => r.Name == "csrss.exe");
        vm.EndTaskCommand.Execute(csrss);
        Assert.False(csrss.IsConfirming);
        await vm.ConfirmEndTaskAsync(csrss);

        Assert.Empty(source.Killed);
        Assert.Contains("protected", vm.Message);
    }

    [Fact]
    public async Task End_task_asks_first_then_ends_that_exact_process()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source, _) = Build(() => clock);
        source.Next = At(1, P(4820, "chrome.exe", created: 77), P(6723, "Discord.exe"));
        await vm.RefreshAsync();
        ProcessRowViewModel chrome = vm.Rows.Single(r => r.Name == "chrome.exe");
        ProcessRowViewModel discord = vm.Rows.Single(r => r.Name == "Discord.exe");

        vm.EndTaskCommand.Execute(discord);
        Assert.True(discord.IsConfirming);
        vm.EndTaskCommand.Execute(chrome);
        Assert.False(discord.IsConfirming);
        Assert.True(chrome.IsConfirming);
        Assert.False(chrome.ShowEndTask);
        Assert.Equal("End chrome.exe? Click Confirm to end it.", vm.Message);

        clock = clock.AddSeconds(1);
        await vm.ConfirmEndTaskAsync(chrome);

        Assert.Equal(new[] { (4820, 77L) }, source.Killed);
        Assert.Equal("Ended chrome.exe", vm.Message);
        Assert.False(chrome.IsConfirming);
    }

    [Fact]
    public async Task Confirm_needs_an_armed_row_and_a_second_separate_click()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source, _) = Build(() => clock);
        source.Next = At(1, P(4820, "chrome.exe"));
        await vm.RefreshAsync();
        ProcessRowViewModel chrome = vm.Rows.Single();

        await vm.ConfirmEndTaskAsync(chrome);
        Assert.Empty(source.Killed);

        vm.EndTaskCommand.Execute(chrome);
        clock = clock.AddMilliseconds(200);
        await vm.ConfirmEndTaskAsync(chrome);
        Assert.Empty(source.Killed);
        Assert.True(chrome.IsConfirming);

        clock = clock.AddMilliseconds(600);
        await vm.ConfirmEndTaskAsync(chrome);
        Assert.Single(source.Killed);
    }

    [Fact]
    public async Task The_order_holds_while_a_row_waits_for_confirm_and_tiny_differences_do_not_reorder()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source, _) = Build(() => clock);
        source.Next = At(1, P(11, "a.exe"), P(12, "b.exe"), P(13, "c.exe"));
        await vm.RefreshAsync();
        source.Next = At(2, P(11, "a.exe", 0.0004), P(12, "b.exe", 0.0008), P(13, "c.exe", 0.4));
        await vm.RefreshAsync();
        Assert.Equal(new[] { "c.exe", "a.exe", "b.exe" }, vm.Rows.Select(r => r.Name));

        vm.EndTaskCommand.Execute(vm.Rows.Single(r => r.Name == "b.exe"));
        source.Next = At(3, P(11, "a.exe", 0.0004), P(12, "b.exe", 0.8), P(13, "c.exe", 0.4));
        await vm.RefreshAsync();
        Assert.Equal(new[] { "c.exe", "a.exe", "b.exe" }, vm.Rows.Select(r => r.Name));
    }

    [Fact]
    public async Task Tweaker_explains_it_cannot_end_itself()
    {
        var (vm, source, _) = Build();
        source.Next = At(1, P(Self, "tweaker.exe"));
        await vm.RefreshAsync();
        vm.EndTaskCommand.Execute(vm.Rows.Single());
        Assert.Equal("Tweaker can't end itself.", vm.Message);
    }

    [Fact]
    public async Task A_failed_end_task_says_why()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source, _) = Build(() => clock);
        source.Next = At(1, P(100, "game.exe"));
        source.KillResult = "access denied";
        await vm.RefreshAsync();

        vm.EndTaskCommand.Execute(vm.Rows.Single());
        clock = clock.AddSeconds(1);
        await vm.ConfirmEndTaskAsync(vm.Rows.Single());

        Assert.Equal("Couldn't end game.exe: access denied", vm.Message);
    }

    [Fact]
    public async Task Rows_are_reused_exited_processes_vanish_and_a_reused_pid_is_a_new_row()
    {
        var (vm, source, _) = Build();
        source.Next = At(1, P(1, "a.exe"), P(2, "b.exe"));
        await vm.RefreshAsync();
        ProcessRowViewModel a = vm.Rows.Single(r => r.Pid == 1);

        source.Next = At(2, P(1, "a.exe"));
        await vm.RefreshAsync();
        Assert.Same(a, vm.Rows.Single());

        source.Next = At(3, P(1, "a.exe", created: 5));
        await vm.RefreshAsync();
        Assert.NotSame(a, vm.Rows.Single());
    }

    [Fact]
    public async Task A_failed_read_keeps_the_last_rows()
    {
        var (vm, source, _) = Build();
        source.Next = At(1, P(1, "a.exe"));
        await vm.RefreshAsync();

        source.Next = ProcessSnapshot.Empty;
        await vm.RefreshAsync();

        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task A_tick_during_a_running_read_is_skipped()
    {
        var source = new GatedProcessSource();
        var vm = new ProcessesViewModel(source, new FakeGpuUsageSource(), Self, WinDir);

        Task first = vm.RefreshAsync();
        Assert.True(source.Entered.Wait(TimeSpan.FromSeconds(5)));
        await vm.RefreshAsync();
        Assert.Equal(1, source.Reads);

        source.Release.Set();
        await first;
    }

    [Fact]
    public void Dispose_releases_the_gpu_counters()
    {
        var (vm, _, gpu) = Build();
        vm.Dispose();
        Assert.True(gpu.Disposed);
    }

    [Fact]
    public async Task The_user_owner_is_marked()
    {
        var (vm, source, _) = Build();
        source.Next = At(1, P(1, "a.exe", owner: "User"), P(2, "b.exe", owner: "SYSTEM"));
        await vm.RefreshAsync();
        Assert.True(vm.Rows.Single(r => r.Pid == 1).IsUserOwner);
        Assert.False(vm.Rows.Single(r => r.Pid == 2).IsUserOwner);
    }

    [Fact]
    public void The_toolbar_shows_the_active_profile_badge()
    {
        var state = new FakeProfileState();
        var vm = new ProcessesViewModel(new FakeProcessSource(), new FakeGpuUsageSource(), 9999, @"C:\Windows", profiles: state);
        Assert.False(vm.ShowProfileBadge);
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        state.Set(true, "FORTNITE PROFILE ACTIVE — 4 APPS CLOSED");

        Assert.True(vm.ShowProfileBadge);
        Assert.Equal("FORTNITE PROFILE ACTIVE — 4 APPS CLOSED", vm.ProfileBadgeText);
        Assert.Contains(nameof(vm.ShowProfileBadge), raised);
    }

    private sealed class GatedProcessSource : IProcessSource
    {
        private int _reads;
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public int Reads => Volatile.Read(ref _reads);

        public ProcessSnapshot Read()
        {
            Interlocked.Increment(ref _reads);
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(10));
            return ProcessSnapshot.Empty;
        }

        public string? Kill(int pid, long createdTicks) => null;
    }
}
