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

public class ServicesViewModelTests
{
    private static ServiceSample S(string name, string display, bool windows = true, ServiceState state = ServiceState.Running,
        ServiceStartup startup = ServiceStartup.Automatic, int pid = 1000, string description = "", bool acceptsStop = true) =>
        new(name, display, description, state, startup, state == ServiceState.Running ? pid : 0, windows, acceptsStop);

    private static ServiceSnapshot Snap(params ServiceSample[] services) => new(services);

    private static (ServicesViewModel vm, FakeServiceSource source) Build(Func<DateTime>? now = null)
    {
        var source = new FakeServiceSource();
        return (new ServicesViewModel(source, now), source);
    }

    [Fact]
    public async Task Rows_show_safety_advice_for_known_services_only()
    {
        var (vm, source) = Build();
        source.Next = Snap(
            S("Spooler", "Print Spooler"),
            S("SysMain", "SysMain"),
            S("WinDefend", "Microsoft Defender Antivirus Service"),
            S("SomeVendorSvc", "Some Vendor Service", windows: false));
        await vm.RefreshAsync();

        ServiceRowViewModel spooler = vm.Rows.Single(r => r.Name == "Spooler");
        Assert.True(spooler.HasAdvice);
        Assert.Equal("SAFE TO STOP", spooler.AdviceText);
        Assert.Equal(Tweaker.Controls.BadgeTone.Low, spooler.AdviceTone);
        Assert.Equal("Only needed for printing.", spooler.AdviceReason);

        ServiceRowViewModel sysmain = vm.Rows.Single(r => r.Name == "SysMain");
        Assert.Equal("CAUTION", sysmain.AdviceText);
        Assert.Equal(Tweaker.Controls.BadgeTone.Medium, sysmain.AdviceTone);

        ServiceRowViewModel defender = vm.Rows.Single(r => r.Name == "WinDefend");
        Assert.Equal("KEEP RUNNING", defender.AdviceText);
        Assert.Equal(Tweaker.Controls.BadgeTone.High, defender.AdviceTone);

        ServiceRowViewModel vendor = vm.Rows.Single(r => r.Name == "SomeVendorSvc");
        Assert.Null(vendor.Advice);
        Assert.False(vendor.HasAdvice);
        Assert.Equal("", vendor.AdviceText);
        Assert.Equal("", vendor.AdviceReason);
    }

    private static (ServicesViewModel vm, FakeServiceSource source, FreedResourcesViewModel freed, List<int> measured)
        BuildWithFreed(Func<DateTime> now, long? workingSet)
    {
        var source = new FakeServiceSource();
        var freed = new FreedResourcesViewModel();
        var measured = new List<int>();
        var vm = new ServicesViewModel(source, now, freed: freed, workingSetOf: pid => { measured.Add(pid); return workingSet; });
        return (vm, source, freed, measured);
    }

    [Fact]
    public async Task Stopping_a_service_records_its_process_memory_and_starting_it_forgets_it()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source, freed, measured) = BuildWithFreed(() => clock, 50L * 1048576);
        source.Next = Snap(S("Spooler", "Print Spooler", pid: 2288), S("SysMain", "SysMain", pid: 3000));
        await vm.RefreshAsync();
        ServiceRowViewModel spooler = vm.Rows.Single(r => r.Name == "Spooler");

        vm.StopCommand.Execute(spooler);
        clock = clock.AddSeconds(1);
        source.Next = Snap(S("Spooler", "Print Spooler", state: ServiceState.Stopped), S("SysMain", "SysMain", pid: 3000));
        await vm.ConfirmStopAsync(spooler);

        Assert.Equal(new[] { 2288 }, measured);
        Assert.Equal(50, freed.MeasuredMb, 3);

        await vm.RefreshAsync();
        Assert.Equal(50, freed.MeasuredMb, 3);
        await vm.StartAsync(spooler);

        Assert.Equal(0, freed.MeasuredMb);
    }

    [Theory]
    [InlineData(ServiceState.Running)]
    [InlineData(ServiceState.StartPending)]
    public async Task A_stopped_service_that_starts_again_elsewhere_stops_counting(ServiceState restarted)
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source, freed, _) = BuildWithFreed(() => clock, 50L * 1048576);
        source.Next = Snap(S("DiagTrack", "Connected User Experiences and Telemetry", pid: 2288));
        await vm.RefreshAsync();
        ServiceRowViewModel row = vm.Rows.Single();

        vm.StopCommand.Execute(row);
        clock = clock.AddSeconds(1);
        source.Next = Snap(S("DiagTrack", "Connected User Experiences and Telemetry", state: ServiceState.Stopped));
        await vm.ConfirmStopAsync(row);
        Assert.Equal(50, freed.MeasuredMb, 3);

        source.Next = Snap(S("diagtrack", "Connected User Experiences and Telemetry", state: restarted, pid: 4100));
        await vm.RefreshAsync();

        Assert.Equal(0, freed.MeasuredMb);
        Assert.Empty(source.Started);
    }

    [Fact]
    public async Task A_read_started_before_the_stop_does_not_forget_it()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var source = new GatedServiceSource { Next = Snap(S("Spooler", "Print Spooler", pid: 2288)) };
        var freed = new FreedResourcesViewModel();
        var vm = new ServicesViewModel(source, () => clock, freed: freed, workingSetOf: _ => 50L * 1048576);
        await vm.RefreshAsync();
        ServiceRowViewModel row = vm.Rows.Single();
        vm.StopCommand.Execute(row);
        clock = clock.AddSeconds(1);

        source.GateReads = true;
        Task staleTick = vm.RefreshAsync();
        Assert.True(source.Entered.Wait(TimeSpan.FromSeconds(5)));
        source.GateReads = false;
        source.GateActions = false;
        await vm.ConfirmStopAsync(row);
        Assert.Equal(50, freed.MeasuredMb, 3);

        source.Release.Set();
        await staleTick;

        Assert.Equal(50, freed.MeasuredMb, 3);
    }

    [Fact]
    public async Task A_protected_row_shows_no_advice_badge()
    {
        var (vm, source) = Build();
        source.Next = Snap(S("Themes", "Themes"), S("WinDefend", "Microsoft Defender Antivirus Service"), S("Spooler", "Print Spooler"));
        await vm.RefreshAsync();

        ServiceRowViewModel themes = vm.Rows.Single(r => r.Name == "Themes");
        Assert.True(themes.IsProtected);
        Assert.NotNull(themes.Advice);
        Assert.False(themes.HasAdvice);
        Assert.Equal("protected", themes.LockText);
        Assert.False(vm.Rows.Single(r => r.Name == "WinDefend").HasAdvice);
        Assert.True(vm.Rows.Single(r => r.Name == "Spooler").HasAdvice);
    }

    [Fact]
    public async Task A_service_sharing_its_process_is_not_measured()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source, freed, measured) = BuildWithFreed(() => clock, 50L * 1048576);
        source.Next = Snap(S("Spooler", "Print Spooler", pid: 1000), S("SysMain", "SysMain", pid: 1000));
        await vm.RefreshAsync();
        ServiceRowViewModel spooler = vm.Rows.Single(r => r.Name == "Spooler");

        vm.StopCommand.Execute(spooler);
        clock = clock.AddSeconds(1);
        await vm.ConfirmStopAsync(spooler);

        Assert.Equal(new[] { "Spooler" }, source.Stopped);
        Assert.Empty(measured);
        Assert.Equal(0, freed.MeasuredMb);
    }

    [Fact]
    public async Task A_failed_stop_records_nothing()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source, freed, _) = BuildWithFreed(() => clock, 50L * 1048576);
        source.StopResult = "Access is denied.";
        source.Next = Snap(S("Spooler", "Print Spooler", pid: 2288));
        await vm.RefreshAsync();
        ServiceRowViewModel spooler = vm.Rows.Single();

        vm.StopCommand.Execute(spooler);
        clock = clock.AddSeconds(1);
        await vm.ConfirmStopAsync(spooler);

        Assert.Equal(0, freed.MeasuredMb);
        Assert.False(freed.HasTotal);
    }

    [Fact]
    public async Task An_unreadable_working_set_records_nothing()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source, freed, measured) = BuildWithFreed(() => clock, null);
        source.Next = Snap(S("Spooler", "Print Spooler", pid: 2288));
        await vm.RefreshAsync();
        ServiceRowViewModel spooler = vm.Rows.Single();

        vm.StopCommand.Execute(spooler);
        clock = clock.AddSeconds(1);
        await vm.ConfirmStopAsync(spooler);

        Assert.Equal(new[] { 2288 }, measured);
        Assert.Equal(new[] { "Spooler" }, source.Stopped);
        Assert.Equal(0, freed.MeasuredMb);
    }

    [Fact]
    public async Task Rows_show_group_status_startup_and_pid_third_party_first()
    {
        var (vm, source) = Build();
        source.Next = Snap(
            S("Spooler", "Print Spooler", pid: 2288),
            S("NVDisplay.ContainerLocalSystem", "NVIDIA Display Container LS", windows: false, pid: 2148),
            S("XblGameSave", "Xbox Live Game Save", state: ServiceState.Stopped, startup: ServiceStartup.Manual),
            S("RemoteRegistry", "Remote Registry", state: ServiceState.Stopped, startup: ServiceStartup.Disabled));
        await vm.RefreshAsync();

        Assert.Equal(new[] { "NVIDIA Display Container LS", "Print Spooler", "Remote Registry", "Xbox Live Game Save" },
            vm.Rows.Select(r => r.DisplayName));
        ServiceRowViewModel nvidia = vm.Rows[0];
        Assert.Equal("THIRD-PARTY", nvidia.GroupTitle);
        Assert.Equal(0, nvidia.GroupOrder);
        Assert.Equal("Running", nvidia.StatusText);
        Assert.True(nvidia.IsRunning);
        Assert.Equal("2148", nvidia.PidText);
        ServiceRowViewModel remote = vm.Rows.Single(r => r.Name == "RemoteRegistry");
        Assert.Equal("WINDOWS", remote.GroupTitle);
        Assert.Equal("Disabled", remote.StartupText);
        Assert.True(remote.IsDisabled);
        Assert.Equal("—", remote.PidText);
        Assert.False(remote.CanStart);
        Assert.Equal("4 services", vm.CountText);
    }

    [Fact]
    public async Task Group_and_status_filters_apply_without_a_new_read()
    {
        var (vm, source) = Build();
        source.Next = Snap(
            S("NVDisplay.ContainerLocalSystem", "NVIDIA Display Container LS", windows: false),
            S("Spooler", "Print Spooler"),
            S("XblGameSave", "Xbox Live Game Save", state: ServiceState.Stopped));
        await vm.RefreshAsync();

        vm.SetGroupCommand.Execute("ThirdParty");
        Assert.True(vm.IsThirdPartyOnly);
        Assert.Equal(new[] { "NVIDIA Display Container LS" }, vm.Rows.Select(r => r.DisplayName));
        Assert.Equal("1 service", vm.CountText);

        vm.SetGroupCommand.Execute("Windows");
        vm.SetStatusCommand.Execute("Stopped");
        Assert.True(vm.IsStoppedOnly);
        Assert.Equal(new[] { "Xbox Live Game Save" }, vm.Rows.Select(r => r.DisplayName));

        vm.SetGroupCommand.Execute("All");
        vm.SetStatusCommand.Execute("Any");
        Assert.True(vm.IsAllGroups);
        Assert.True(vm.IsAnyStatus);
        Assert.Equal(3, vm.Rows.Count);
        Assert.Equal(1, source.Reads);
    }

    [Fact]
    public async Task The_text_filter_matches_display_name_service_name_or_description()
    {
        var (vm, source) = Build();
        source.Next = Snap(
            S("Spooler", "Print Spooler", description: "Spools print jobs"),
            S("SysMain", "SysMain", description: "Maintains and improves system performance"));
        await vm.RefreshAsync();

        vm.Filter = "print";
        Assert.Equal(new[] { "Spooler" }, vm.Rows.Select(r => r.Name));
        vm.Filter = "sysmain";
        Assert.Equal(new[] { "SysMain" }, vm.Rows.Select(r => r.Name));
        vm.Filter = "PERFORMANCE";
        Assert.Equal(new[] { "SysMain" }, vm.Rows.Select(r => r.Name));
        vm.Filter = "";
        Assert.Equal(2, vm.Rows.Count);
    }

    [Fact]
    public async Task Start_runs_the_service_and_reports_the_result()
    {
        var (vm, source) = Build();
        source.Next = Snap(S("XblGameSave", "Xbox Live Game Save", state: ServiceState.Stopped, startup: ServiceStartup.Manual));
        await vm.RefreshAsync();
        ServiceRowViewModel row = vm.Rows.Single();
        Assert.True(row.ShowStart);
        Assert.True(row.CanStart);

        await vm.StartAsync(row);
        Assert.Equal(new[] { "XblGameSave" }, source.Started);
        Assert.Equal("Started Xbox Live Game Save", vm.Message);

        source.StartResult = "The service did not respond to the start or control request in a timely fashion.";
        await vm.StartAsync(row);
        Assert.Equal("Couldn't start Xbox Live Game Save: The service did not respond to the start or control request in a timely fashion.", vm.Message);
    }

    [Fact]
    public async Task A_disabled_service_is_never_started()
    {
        var (vm, source) = Build();
        source.Next = Snap(S("RemoteRegistry", "Remote Registry", state: ServiceState.Stopped, startup: ServiceStartup.Disabled));
        await vm.RefreshAsync();

        await vm.StartAsync(vm.Rows.Single());

        Assert.Empty(source.Started);
        Assert.Equal("Remote Registry is disabled — set its startup type first.", vm.Message);
    }

    [Fact]
    public async Task Stop_needs_an_armed_row_and_a_second_separate_click()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source) = Build(() => clock);
        source.Next = Snap(S("Spooler", "Print Spooler"), S("SysMain", "SysMain"));
        await vm.RefreshAsync();
        ServiceRowViewModel spooler = vm.Rows.Single(r => r.Name == "Spooler");
        ServiceRowViewModel sysmain = vm.Rows.Single(r => r.Name == "SysMain");

        await vm.ConfirmStopAsync(spooler);
        Assert.Empty(source.Stopped);

        vm.StopCommand.Execute(sysmain);
        vm.StopCommand.Execute(spooler);
        Assert.False(sysmain.IsConfirming);
        Assert.True(spooler.IsConfirming);
        Assert.False(spooler.ShowStop);
        Assert.True(spooler.ShowConfirm);
        Assert.Equal("Stop Print Spooler? Click Confirm to stop it.", vm.Message);

        clock = clock.AddMilliseconds(200);
        await vm.ConfirmStopAsync(spooler);
        Assert.Empty(source.Stopped);

        clock = clock.AddSeconds(1);
        await vm.ConfirmStopAsync(spooler);
        Assert.Equal(new[] { "Spooler" }, source.Stopped);
        Assert.Equal("Stopped Print Spooler", vm.Message);
        Assert.False(spooler.IsConfirming);
    }

    [Fact]
    public async Task Protected_and_unstoppable_services_are_never_stopped()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source) = Build(() => clock);
        source.Next = Snap(S("RpcSs", "Remote Procedure Call (RPC)"), S("Vendor", "Vendor Helper", windows: false, acceptsStop: false));
        await vm.RefreshAsync();
        ServiceRowViewModel rpc = vm.Rows.Single(r => r.Name == "RpcSs");
        ServiceRowViewModel vendor = vm.Rows.Single(r => r.Name == "Vendor");

        Assert.Equal("protected", rpc.LockText);
        Assert.True(rpc.ShowLock);
        Assert.False(rpc.ShowStop);
        Assert.Equal("can't stop", vendor.LockText);
        Assert.False(vendor.ShowStop);

        vm.StopCommand.Execute(rpc);
        Assert.False(rpc.IsConfirming);
        Assert.Contains("protected", vm.Message);
        vm.StopCommand.Execute(vendor);
        Assert.False(vendor.IsConfirming);
        Assert.Equal("Vendor Helper can't be stopped — Windows doesn't allow it.", vm.Message);

        clock = clock.AddSeconds(1);
        await vm.ConfirmStopAsync(rpc);
        Assert.Empty(source.Stopped);
    }

    [Fact]
    public async Task A_failed_stop_says_why()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source) = Build(() => clock);
        source.Next = Snap(S("Spooler", "Print Spooler"));
        source.StopResult = "A stop control has been sent to a service that other running services are dependent on.";
        await vm.RefreshAsync();
        ServiceRowViewModel row = vm.Rows.Single();

        vm.StopCommand.Execute(row);
        clock = clock.AddSeconds(1);
        await vm.ConfirmStopAsync(row);

        Assert.Equal("Couldn't stop Print Spooler: A stop control has been sent to a service that other running services are dependent on.", vm.Message);
    }

    [Fact]
    public async Task A_row_is_busy_while_its_action_runs()
    {
        var source = new GatedServiceSource
        {
            Next = Snap(S("XblGameSave", "Xbox Live Game Save", state: ServiceState.Stopped, startup: ServiceStartup.Manual)),
        };
        var vm = new ServicesViewModel(source);
        await vm.RefreshAsync();
        ServiceRowViewModel row = vm.Rows.Single();

        Task start = vm.StartAsync(row);
        Assert.True(source.Entered.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(row.IsBusy);
        Assert.False(row.ShowStart);
        Assert.Equal("Starting Xbox Live Game Save…", vm.Message);

        source.Release.Set();
        await start;
        Assert.False(row.IsBusy);
    }

    [Fact]
    public async Task Rows_are_reused_and_follow_state_changes()
    {
        var (vm, source) = Build();
        source.Next = Snap(S("Spooler", "Print Spooler"));
        await vm.RefreshAsync();
        ServiceRowViewModel row = vm.Rows.Single();
        Assert.True(row.ShowStop);

        source.Next = Snap(S("Spooler", "Print Spooler", state: ServiceState.Stopped));
        await vm.RefreshAsync();

        Assert.Same(row, vm.Rows.Single());
        Assert.Equal("Stopped", row.StatusText);
        Assert.True(row.ShowStart);
        Assert.False(row.ShowStop);
    }

    [Fact]
    public async Task A_failed_read_keeps_the_last_rows()
    {
        var (vm, source) = Build();
        source.Next = Snap(S("Spooler", "Print Spooler"));
        await vm.RefreshAsync();

        source.Next = ServiceSnapshot.Empty;
        await vm.RefreshAsync();

        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task A_tick_during_a_running_read_is_skipped()
    {
        var source = new GatedServiceSource { GateReads = true };
        var vm = new ServicesViewModel(source);

        Task first = vm.RefreshAsync();
        Assert.True(source.Entered.Wait(TimeSpan.FromSeconds(5)));
        await vm.RefreshAsync();
        Assert.Equal(1, source.Reads);

        source.Release.Set();
        await first;
    }

    [Fact]
    public async Task An_armed_row_whose_service_stops_on_its_own_is_disarmed()
    {
        DateTime clock = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (vm, source) = Build(() => clock);
        source.Next = Snap(S("Spooler", "Print Spooler"));
        await vm.RefreshAsync();
        ServiceRowViewModel row = vm.Rows.Single();
        vm.StopCommand.Execute(row);
        Assert.True(row.ShowConfirm);

        source.Next = Snap(S("Spooler", "Print Spooler", state: ServiceState.Stopped));
        await vm.RefreshAsync();

        Assert.False(row.IsConfirming);
        Assert.False(row.ShowConfirm);
        Assert.True(row.ShowStart);
        clock = clock.AddSeconds(1);
        await vm.ConfirmStopAsync(row);
        Assert.Empty(source.Stopped);
    }

    [Fact]
    public async Task A_stopped_protected_service_can_be_started_but_not_stopped()
    {
        var (vm, source) = Build();
        source.Next = Snap(S("MpsSvc", "Windows Defender Firewall", state: ServiceState.Stopped));
        await vm.RefreshAsync();
        ServiceRowViewModel row = vm.Rows.Single();

        Assert.True(row.ShowStart);
        Assert.False(row.ShowLock);
        await vm.StartAsync(row);
        Assert.Equal(new[] { "MpsSvc" }, source.Started);

        source.Next = Snap(S("MpsSvc", "Windows Defender Firewall"));
        await vm.RefreshAsync();
        Assert.Equal("protected", row.LockText);
        Assert.False(row.ShowStop);
    }

    [Fact]
    public async Task An_unchanged_read_raises_no_row_notifications()
    {
        var (vm, source) = Build();
        source.Next = Snap(S("Spooler", "Print Spooler"));
        await vm.RefreshAsync();
        ServiceRowViewModel row = vm.Rows.Single();
        int raised = 0;
        row.PropertyChanged += (_, _) => raised++;

        source.Next = Snap(S("Spooler", "Print Spooler"));
        await vm.RefreshAsync();

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task Rows_the_active_profile_manages_are_tagged()
    {
        var state = new FakeProfileState();
        var source = new FakeServiceSource { Next = Snap(S("wuauserv", "Windows Update"), S("Spooler", "Print Spooler")) };
        var vm = new ServicesViewModel(source, profiles: state);
        await vm.RefreshAsync();
        Assert.All(vm.Rows, r => Assert.False(r.IsProfileManaged));

        state.Set(true, "GAMING MODE PROFILE ACTIVE — 2 APPS CLOSED", "Windows Update");
        Assert.True(vm.Rows.Single(r => r.Name == "wuauserv").IsProfileManaged);
        Assert.False(vm.Rows.Single(r => r.Name == "Spooler").IsProfileManaged);

        source.Next = Snap(S("wuauserv", "Windows Update", state: ServiceState.Stopped), S("Spooler", "Print Spooler"));
        await vm.RefreshAsync();
        Assert.True(vm.Rows.Single(r => r.Name == "wuauserv").IsProfileManaged);

        state.Set(false);
        Assert.All(vm.Rows, r => Assert.False(r.IsProfileManaged));
    }

    private sealed class GatedServiceSource : IServiceSource
    {
        private int _reads;
        public ServiceSnapshot Next { get; set; } = ServiceSnapshot.Empty;
        public bool GateReads { get; set; }
        public bool GateActions { get; set; } = true;
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public int Reads => Volatile.Read(ref _reads);

        public ServiceSnapshot Read()
        {
            Interlocked.Increment(ref _reads);
            if (GateReads) Gate();
            return Next;
        }

        public string? Start(string name) { if (GateActions) Gate(); return null; }
        public string? Stop(string name) { if (GateActions) Gate(); return null; }

        private void Gate()
        {
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(10));
        }
    }
}
