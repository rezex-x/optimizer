using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class ResourcesViewModelTests
{
    private static (ResourcesViewModel vm, FakeHardwareMonitor monitor, FakeGpuUsageSource gpu) Build(FakeProfileStore? store = null)
    {
        var monitor = new FakeHardwareMonitor();
        var gpu = new FakeGpuUsageSource();
        var manager = new ProfileManager(store ?? new FakeProfileStore(), new FakeProcessSource(), new FakeServiceSource(),
            new FakeProcessCloser(), new FakeRunningExeSource(), 9999, @"C:\Windows", _ => null);
        var vm = new ResourcesViewModel(
            new ProfilesViewModel(manager),
            new PerformanceViewModel(monitor, new FakeSystemInfoSource(), new DpcSampleViewModel(new FakeDpcDiagnostic())),
            new NetworkViewModel(new FakeNetworkAdapterSource(), new FakeConnectionSource(), new FakeNetworkDiagnostic()),
            new ProcessesViewModel(new FakeProcessSource(), gpu, 9999, @"C:\Windows"),
            new ServicesViewModel(new FakeServiceSource()));
        return (vm, monitor, gpu);
    }

    [Fact]
    public void Defaults_to_Performance()
    {
        var (vm, _, _) = Build();
        Assert.Equal(ResourceTab.Performance, vm.Selected);
        Assert.True(vm.IsPerformance);
        Assert.False(vm.IsNetwork);
        Assert.IsType<PerformanceViewModel>(vm.Current);
    }

    [Fact]
    public void Selecting_Network_shows_the_network_screen_and_notifies()
    {
        var (vm, _, _) = Build();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.SelectTabCommand.Execute("Network");

        Assert.Equal(ResourceTab.Network, vm.Selected);
        Assert.True(vm.IsNetwork);
        Assert.False(vm.IsPerformance);
        Assert.IsType<NetworkViewModel>(vm.Current);
        Assert.Contains(nameof(vm.Current), raised);
    }

    [Fact]
    public void Unknown_sub_tab_name_is_ignored()
    {
        var (vm, _, _) = Build();
        vm.SelectTabCommand.Execute("Nope");
        Assert.Equal(ResourceTab.Performance, vm.Selected);
    }

    [Fact]
    public void Dispose_releases_the_hardware_monitor_and_the_gpu_counters()
    {
        var (vm, monitor, gpu) = Build();
        vm.Dispose();
        Assert.True(monitor.Disposed);
        Assert.True(gpu.Disposed);
    }

    [Fact]
    public void Selecting_Processes_shows_the_processes_screen()
    {
        var (vm, _, _) = Build();
        vm.SelectTabCommand.Execute("Processes");
        Assert.Equal(ResourceTab.Processes, vm.Selected);
        Assert.True(vm.IsProcesses);
        Assert.False(vm.IsPerformance);
        Assert.IsType<ProcessesViewModel>(vm.Current);
    }

    [Fact]
    public void Selecting_Services_shows_the_services_screen()
    {
        var (vm, _, _) = Build();
        vm.SelectTabCommand.Execute("Services");
        Assert.Equal(ResourceTab.Services, vm.Selected);
        Assert.True(vm.IsServices);
        Assert.IsType<ServicesViewModel>(vm.Current);
    }

    [Fact]
    public void Selecting_App_Profiles_shows_the_profiles_screen()
    {
        var (vm, _, _) = Build();
        vm.SelectTabCommand.Execute("Profiles");
        Assert.Equal(ResourceTab.Profiles, vm.Selected);
        Assert.True(vm.IsProfiles);
        Assert.IsType<ProfilesViewModel>(vm.Current);
    }

    [Fact]
    public async Task The_profile_dot_follows_the_active_profile()
    {
        var store = new FakeProfileStore
        {
            Session = new ProfileSession("gaming", "Gaming Mode", DateTime.UtcNow, false, false,
                Array.Empty<string>(), Array.Empty<string>(), 0),
        };
        var (vm, _, _) = Build(store);
        Assert.True(vm.IsProfileActive);
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        await vm.Profiles.Manager.DeactivateAsync(restore: false);

        Assert.False(vm.IsProfileActive);
        Assert.Contains(nameof(vm.IsProfileActive), raised);
    }
}
