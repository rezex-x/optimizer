using Tweaker.Core.Nvidia;
using Tweaker.Core.Services;
using Tweaker.Shell;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.Shell;

public class ShellViewModelTests
{
    private static ShellViewModel Build(Tweaker.Services.GpuInfo? gpu = null, FakeProfileStore? store = null,
        PendingChangesViewModel? pending = null, FreedResourcesViewModel? freed = null)
    {
        using var temp = new TempDir();
        string path = System.IO.Path.Combine(temp.Path, "catalog.json");
        System.IO.File.WriteAllText(path, "[]");
        var confirm = new FakeConfirmationService();
        var tweaks = new MainViewModel(new CatalogService(new FileCatalogSource(path)), new FakeTweakEngine(), confirm);
        var nvidia = new NvidiaViewModel(new FakeNvidiaSettingsService(),
            new NvidiaBackupStore(System.IO.Path.Combine(temp.Path, "nvidia-backup.json")),
            confirm, Tweaker.Core.Nvidia.NvidiaSettingsCatalog.Items());
        var manager = new ProfileManager(store ?? new FakeProfileStore(), new FakeProcessSource(), new FakeServiceSource(),
            new FakeProcessCloser(), new FakeRunningExeSource(), 9999, @"C:\Windows", _ => null);
        var resources = new ResourcesViewModel(
            new ProfilesViewModel(manager),
            new PerformanceViewModel(new FakeHardwareMonitor(), new FakeSystemInfoSource(), new DpcSampleViewModel(new FakeDpcDiagnostic())),
            new NetworkViewModel(new FakeNetworkAdapterSource(), new FakeConnectionSource(), new FakeNetworkDiagnostic()),
            new ProcessesViewModel(new FakeProcessSource(), new FakeGpuUsageSource(), 9999, @"C:\Windows"),
            new ServicesViewModel(new FakeServiceSource()));
        return new ShellViewModel(tweaks, new InstallViewModel(new FakeWingetService()),
            new ConfigViewModel(new System.Collections.Generic.List<Tweaker.Core.Models.Tweak>(), new FakeTweakEngine(), confirm),
            new UpdatesViewModel(new FakeWindowsUpdateService()), nvidia,
            gpu ?? new Tweaker.Services.GpuInfo(true, false, false, new[] { "NVIDIA" }),
            resources,
            new HardwareViewModel(new FakeDeviceSource(), new FakeDevicePowerBackup(), new StorageViewModel(new FakeStorageDiagnostic())),
            new InspectorViewModel(new FakeConfigFileStore(), new FakeRunningExeSource(), () => System.Array.Empty<Tweaker.Services.DetectedConfig>()),
            pending, freed: freed);
    }

    [Fact]
    public void Defaults_to_tweaks_tab()
    {
        var vm = Build();
        Assert.Equal(Tab.Tweaks, vm.Selected);
        Assert.True(vm.IsTweaks);
        Assert.IsType<MainViewModel>(vm.Current);
    }

    [Fact]
    public void SelectTabCommand_switches_tab_and_current()
    {
        var vm = Build();
        vm.SelectTabCommand.Execute("Install");
        Assert.Equal(Tab.Install, vm.Selected);
        Assert.True(vm.IsInstall);
        Assert.False(vm.IsTweaks);
        Assert.IsType<InstallViewModel>(vm.Current);

        vm.SelectTabCommand.Execute("Updates");
        Assert.Equal(Tab.Updates, vm.Selected);
        Assert.IsType<UpdatesViewModel>(vm.Current);
    }

    [Fact]
    public void SelectTabCommand_switches_to_nvidia()
    {
        var vm = Build();
        vm.SelectTabCommand.Execute("Nvidia");
        Assert.Equal(Tab.Nvidia, vm.Selected);
        Assert.True(vm.IsNvidia);
        Assert.IsType<NvidiaViewModel>(vm.Current);
    }

    [Fact]
    public void Default_tab_is_Tweaks()
    {
        Assert.Equal("Tweaks", Tab.Tweaks.ToString());
        Assert.Equal(0, (int)Tab.Tweaks);
    }

    [Fact]
    public void Every_tab_name_parses()
    {
        foreach (string name in new[] { "Tweaks", "Nvidia", "Config", "Install", "Updates", "Resources" })
            Assert.True(System.Enum.TryParse<Tab>(name, out _), name);
    }

    [Fact]
    public void Monitor_and_Diagnostics_are_no_longer_top_level_tabs()
    {
        Assert.False(System.Enum.TryParse<Tab>("Monitor", out _));
        Assert.False(System.Enum.TryParse<Tab>("Diagnostics", out _));
    }

    [Fact]
    public void Nvidia_tab_hidden_when_no_nvidia_gpu()
    {
        var vm = Build(new Tweaker.Services.GpuInfo(false, true, false, new[] { "AMD Radeon RX 7800 XT" }));
        Assert.False(vm.IsNvidiaVisible);
    }

    [Fact]
    public void Nvidia_tab_shown_when_nvidia_present()
    {
        var vm = Build(new Tweaker.Services.GpuInfo(true, false, false, new[] { "NVIDIA GeForce RTX 4060 Ti" }));
        Assert.True(vm.IsNvidiaVisible);
    }

    [Fact]
    public void Resources_tab_selects_the_resources_hub()
    {
        var vm = Build();
        vm.SelectTabCommand.Execute("Resources");
        Assert.Equal(Tab.Resources, vm.Selected);
        Assert.True(vm.IsResources);
        Assert.IsType<ResourcesViewModel>(vm.Current);
    }

    [Fact]
    public async Task The_resources_dot_follows_the_active_profile()
    {
        var store = new FakeProfileStore
        {
            Session = new Tweaker.Services.ProfileSession("gaming", "Gaming Mode", System.DateTime.UtcNow, false, false,
                System.Array.Empty<string>(), System.Array.Empty<string>(), 0),
        };
        var vm = Build(store: store);
        Assert.True(vm.IsProfileActive);
        var raised = new System.Collections.Generic.List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        await vm.Resources.Profiles.Manager.DeactivateAsync(restore: false);

        Assert.False(vm.IsProfileActive);
        Assert.Contains(nameof(vm.IsProfileActive), raised);
    }

    [Fact]
    public void Selecting_Hardware_shows_the_hardware_tab()
    {
        var vm = Build();
        vm.SelectTabCommand.Execute("Hardware");
        Assert.Equal(Tab.Hardware, vm.Selected);
        Assert.True(vm.IsHardware);
        Assert.False(vm.IsResources);
        Assert.IsType<HardwareViewModel>(vm.Current);
    }

    [Fact]
    public void Selecting_Inspector_shows_the_inspector_tab()
    {
        var vm = Build();
        vm.SelectTabCommand.Execute("Inspector");
        Assert.Equal(Tab.Inspector, vm.Selected);
        Assert.True(vm.IsInspector);
        Assert.False(vm.IsHardware);
        Assert.IsType<InspectorViewModel>(vm.Current);
    }

    [Fact]
    public void The_tip_follows_the_selected_tab()
    {
        var vm = Build();
        Assert.Equal(TabTips.For(Tab.Tweaks, null), vm.Tip);
        var raised = new System.Collections.Generic.List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.SelectTabCommand.Execute("Updates");

        Assert.Equal(TabTips.For(Tab.Updates, null), vm.Tip);
        Assert.Contains(nameof(vm.Tip), raised);
    }

    [Fact]
    public void The_tip_follows_the_resources_sub_tab()
    {
        var vm = Build();
        vm.SelectTabCommand.Execute("Resources");
        Assert.Equal(TabTips.For(Tab.Resources, vm.Resources.Selected), vm.Tip);
        var raised = new System.Collections.Generic.List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.Resources.SelectTabCommand.Execute("Services");

        Assert.Equal(TabTips.For(Tab.Resources, ResourceTab.Services), vm.Tip);
        Assert.StartsWith("Stop never stops", vm.Tip);
        Assert.Contains(nameof(vm.Tip), raised);
    }

    [Fact]
    public void Pending_is_never_null()
    {
        Assert.NotNull(Build().Pending);
        var pending = new PendingChangesViewModel();
        Assert.Same(pending, Build(pending: pending).Pending);
    }

    [Fact]
    public void Activity_lines_are_empty_without_an_activity_log()
    {
        Assert.Empty(Build().ActivityLines);
    }

    [Fact]
    public void Freed_is_never_null()
    {
        Assert.NotNull(Build().Freed);
        var freed = new FreedResourcesViewModel();
        Assert.Same(freed, Build(freed: freed).Freed);
    }
}
