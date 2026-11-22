using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class DeviceAdvancedViewModelTests
{
    private const string Gpu = @"PCI\VEN_10DE&DEV_2803\4&1";
    private const string Nic = @"PCI\VEN_8086&DEV_15F3\6&2";
    private readonly FakeRegistryService _registry = new();
    private readonly FakeNicPropertySource _nics = new();
    private readonly FakeAdvancedBackup _backup = new();

    private DeviceAdvancedViewModel Build() => new(new InterruptSettings(_registry), _nics, _backup, 4);

    private static DeviceRowViewModel Row(string id, string name, string cls) =>
        new(new DeviceSample(id, name, cls, "Maker", 0, null), DeviceRules.Group(cls, 0)!);

    private static NicInfo Adapter() => new("Ethernet", new[]
    {
        new NicProperty("*EEE", "Energy-Efficient Ethernet", "1", new[] { new ChoiceOption("0", "Disabled"), new ChoiceOption("1", "Enabled") }),
        new NicProperty("*InterruptModeration", "Interrupt Moderation", "1", new[] { new ChoiceOption("0", "Disabled"), new ChoiceOption("1", "Enabled") }),
    });

    [Fact]
    public async Task Opening_a_pci_device_shows_its_interrupt_settings()
    {
        _registry.Values["HKLM|" + InterruptRules.MsiPath(Gpu) + "|MSISupported"] = (RegistryValueKind.DWord, 1);
        DeviceAdvancedViewModel vm = Build();

        await vm.OpenAsync(Row(Gpu, "RTX 4060 Ti", "Display"));

        Assert.True(vm.IsOpen);
        Assert.Equal("RTX 4060 Ti", vm.DeviceName);
        Assert.True(vm.ShowInterrupts);
        Assert.False(vm.ShowNic);
        Assert.Equal("On", vm.SelectedMsi);
        Assert.Equal("Windows default", vm.SelectedAffinity!.Label);
        Assert.Equal(6, vm.AffinityOptions.Count);
        Assert.False(vm.CanRevert);
    }

    [Fact]
    public async Task Changing_msi_writes_it_remembers_the_original_and_forgets_it_when_set_back()
    {
        DeviceAdvancedViewModel vm = Build();
        await vm.OpenAsync(Row(Gpu, "RTX", "Display"));

        vm.SelectedMsi = "Off";

        Assert.Contains(_registry.Writes, w => w.name == "MSISupported" && (int)w.value == 0);
        Assert.Equal("", _backup.Originals["msi|" + Gpu]);
        Assert.Equal("Message-signaled interrupts set to Off — restart to apply.", vm.Message);
        Assert.True(vm.CanRevert);

        vm.SelectedMsi = "Not set (line-based)";
        Assert.Contains(("HKLM", InterruptRules.MsiPath(Gpu), (string?)"MSISupported"), _registry.Deletes);
        Assert.False(vm.CanRevert);
    }

    [Fact]
    public async Task Pinning_to_one_cpu_writes_the_policy_and_mask()
    {
        DeviceAdvancedViewModel vm = Build();
        await vm.OpenAsync(Row(Gpu, "RTX", "Display"));

        vm.SelectedAffinity = vm.AffinityOptions.Single(o => o.Label == "CPU 2");

        Assert.Contains(_registry.Writes, w => w.name == "DevicePolicy" && (int)w.value == 4);
        Assert.Contains(_registry.Writes, w => w.name == "AssignmentSetOverride" && ((byte[])w.value)[0] == 4);
        Assert.Equal("", _backup.Originals["affinity|" + Gpu]);
        Assert.Equal("Interrupt affinity set to CPU 2 — restart to apply.", vm.Message);
        Assert.Equal("CPU 2", vm.SelectedAffinity!.Label);
    }

    [Fact]
    public async Task A_policy_tweaker_does_not_offer_shows_as_custom()
    {
        _registry.Values["HKLM|" + InterruptRules.AffinityPath(Gpu) + "|DevicePolicy"] = (RegistryValueKind.DWord, 3);
        DeviceAdvancedViewModel vm = Build();

        await vm.OpenAsync(Row(Gpu, "RTX", "Display"));

        Assert.Equal("Custom (policy 3)", vm.SelectedAffinity!.Label);
        Assert.Equal(7, vm.AffinityOptions.Count);
        Assert.Contains(vm.SelectedAffinity, vm.AffinityOptions);
    }

    [Fact]
    public async Task Other_devices_have_nothing_to_tune()
    {
        DeviceAdvancedViewModel vm = Build();
        await vm.OpenAsync(Row(@"USB\VID_1\2", "Hub", "USB"));
        Assert.False(vm.ShowInterrupts);
        Assert.False(vm.ShowNic);
        Assert.Empty(_nics.Reads);
    }

    [Fact]
    public async Task Network_adapters_list_their_supported_properties()
    {
        _nics.Next = Adapter();
        DeviceAdvancedViewModel vm = Build();

        await vm.OpenAsync(Row(Nic, "Intel I225-V", "Net"));

        Assert.True(vm.ShowNic);
        Assert.True(vm.ShowInterrupts);
        Assert.Equal(new[] { Nic }, _nics.Reads);
        Assert.Equal(new[] { "Energy-Efficient Ethernet", "Interrupt Moderation" }, vm.NicProperties.Select(p => p.DisplayName));
        Assert.All(vm.NicProperties, p => Assert.Equal("Enabled", p.Selected!.Label));
        Assert.Contains("wake-up latency", vm.NicProperties[0].Hint);
        Assert.False(vm.IsLoadingNic);
        Assert.False(vm.HasNicNote);
    }

    [Fact]
    public async Task Changing_an_adapter_property_sets_it_and_remembers_the_original()
    {
        _nics.Next = Adapter();
        DeviceAdvancedViewModel vm = Build();
        await vm.OpenAsync(Row(Nic, "Intel I225-V", "Net"));
        NicPropertyViewModel eee = vm.NicProperties[0];

        await vm.SetNicAsync(eee, eee.Options[0]);

        Assert.Equal(new[] { ("Ethernet", "*EEE", "0") }, _nics.Sets);
        Assert.Equal("Disabled", eee.Selected!.Label);
        Assert.Equal("1", _backup.Originals["nic|" + Nic + "|*EEE"]);
        Assert.Equal("Energy-Efficient Ethernet set to Disabled — the adapter reconnects for a few seconds.", vm.Message);
    }

    [Fact]
    public async Task A_failed_adapter_change_snaps_back_and_says_why()
    {
        _nics.Next = Adapter();
        _nics.SetResult = "Access is denied.";
        DeviceAdvancedViewModel vm = Build();
        await vm.OpenAsync(Row(Nic, "Intel I225-V", "Net"));
        NicPropertyViewModel eee = vm.NicProperties[0];

        await vm.SetNicAsync(eee, eee.Options[0]);

        Assert.Equal("Enabled", eee.Selected!.Label);
        Assert.Equal("Couldn't change Energy-Efficient Ethernet: Access is denied.", vm.Message);
        Assert.False(vm.CanRevert);
    }

    [Fact]
    public async Task An_unreadable_or_empty_adapter_says_so()
    {
        DeviceAdvancedViewModel vm = Build();
        await vm.OpenAsync(Row(Nic, "Intel I225-V", "Net"));
        Assert.Equal("Couldn't read this adapter's properties.", vm.NicNote);
        Assert.True(vm.HasNicNote);

        _nics.Next = new NicInfo("Ethernet", System.Array.Empty<NicProperty>());
        await vm.OpenAsync(Row(Nic, "Intel I225-V", "Net"));
        Assert.Equal("No tunable properties found for this adapter.", vm.NicNote);
    }

    [Fact]
    public async Task Revert_puts_this_devices_originals_back()
    {
        _nics.Next = Adapter();
        DeviceAdvancedViewModel vm = Build();
        await vm.OpenAsync(Row(Nic, "Intel I225-V", "Net"));
        vm.SelectedMsi = "On";
        vm.SelectedAffinity = vm.AffinityOptions.Single(o => o.Label == "CPU 1");
        await vm.SetNicAsync(vm.NicProperties[0], vm.NicProperties[0].Options[0]);
        _registry.Deletes.Clear();

        await vm.RevertAsync();

        Assert.Contains(("HKLM", InterruptRules.MsiPath(Nic), (string?)"MSISupported"), _registry.Deletes);
        Assert.Contains(("HKLM", InterruptRules.AffinityPath(Nic), (string?)"DevicePolicy"), _registry.Deletes);
        Assert.Equal(("Ethernet", "*EEE", "1"), _nics.Sets.Last());
        Assert.Empty(_backup.Originals);
        Assert.False(vm.CanRevert);
        Assert.Equal("Restored 3 settings. Restart to apply.", vm.Message);
        Assert.Equal("Not set (line-based)", vm.SelectedMsi);
        Assert.Equal("Windows default", vm.SelectedAffinity!.Label);
        Assert.Equal("Enabled", vm.NicProperties[0].Selected!.Label);
    }

    [Fact]
    public async Task Close_hides_the_panel()
    {
        DeviceAdvancedViewModel vm = Build();
        await vm.OpenAsync(Row(Gpu, "RTX", "Display"));
        vm.CloseCommand.Execute(null);
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public async Task A_nic_change_in_flight_blocks_opening_another_device_and_changing_msi()
    {
        var gated = new GatedNicPropertySource { Next = Adapter() };
        var vm = new DeviceAdvancedViewModel(new InterruptSettings(_registry), gated, _backup, 4);
        await vm.OpenAsync(Row(Nic, "Intel I225-V", "Net"));
        NicPropertyViewModel eee = vm.NicProperties[0];

        Task setTask = vm.SetNicAsync(eee, eee.Options[0]);
        Assert.True(gated.Entered.Wait(TimeSpan.FromSeconds(5)));

        vm.CloseCommand.Execute(null);
        await vm.OpenAsync(Row(Gpu, "RTX", "Display"));
        Assert.Equal("Intel I225-V", vm.DeviceName);
        Assert.Equal("Wait for the current change to finish.", vm.Message);
        Assert.True(vm.IsOpen);

        vm.SelectedMsi = "Off";
        Assert.DoesNotContain(_registry.Writes, w => w.name == "MSISupported");

        gated.Release();
        await setTask;
        Assert.Equal(new[] { ("Ethernet", "*EEE", "0") }, gated.Sets);
    }

    [Fact]
    public async Task Revert_of_a_partial_failure_keeps_the_failed_original_and_reports_it()
    {
        _nics.Next = Adapter();
        DeviceAdvancedViewModel vm = Build();
        await vm.OpenAsync(Row(Nic, "Intel I225-V", "Net"));
        vm.SelectedMsi = "On";
        await vm.SetNicAsync(vm.NicProperties[0], vm.NicProperties[0].Options[0]);
        _nics.SetResult = "Access is denied.";

        await vm.RevertAsync();

        Assert.StartsWith("Restored 1 setting.", vm.Message);
        Assert.Contains("couldn't be restored", vm.Message);
        Assert.DoesNotContain("msi|" + Nic, _backup.Originals.Keys);
        Assert.Contains("nic|" + Nic + "|*EEE", _backup.Originals.Keys);
    }

    [Fact]
    public async Task Storage_controllers_hide_interrupt_settings_and_say_why()
    {
        DeviceAdvancedViewModel vm = Build();

        await vm.OpenAsync(Row(@"PCI\VEN_144D&DEV_A808\4&3", "Samsung NVMe", "SCSIAdapter"));

        Assert.True(vm.IsOpen);
        Assert.False(vm.ShowInterrupts);
        Assert.True(vm.HasInterruptNote);
        Assert.Equal("Interrupt settings are hidden for storage controllers: a wrong setting can stop Windows from booting.", vm.InterruptNote);

        await vm.OpenAsync(Row(Gpu, "RTX", "Display"));
        Assert.True(vm.ShowInterrupts);
        Assert.Equal("", vm.InterruptNote);
        Assert.False(vm.HasInterruptNote);
    }

    [Fact]
    public async Task A_change_is_refused_when_its_backup_cant_be_saved()
    {
        _nics.Next = Adapter();
        DeviceAdvancedViewModel vm = Build();
        await vm.OpenAsync(Row(Nic, "Intel I225-V", "Net"));
        _backup.FailRemember = true;

        vm.SelectedMsi = "Off";

        Assert.DoesNotContain(_registry.Writes, w => w.name == "MSISupported");
        Assert.Equal("Couldn't save a backup of the current value — nothing was changed.", vm.Message);
        Assert.Equal("Not set (line-based)", vm.SelectedMsi);
        Assert.False(vm.CanRevert);

        vm.SelectedAffinity = vm.AffinityOptions.Single(o => o.Label == "CPU 1");
        Assert.DoesNotContain(_registry.Writes, w => w.name == "DevicePolicy");
        Assert.Equal("Windows default", vm.SelectedAffinity!.Label);

        NicPropertyViewModel eee = vm.NicProperties[0];
        await vm.SetNicAsync(eee, eee.Options[0]);
        Assert.Empty(_nics.Sets);
        Assert.Equal("Enabled", eee.Selected!.Label);
        Assert.Equal("Couldn't save a backup of the current value — nothing was changed.", vm.Message);
    }

    [Fact]
    public async Task Revert_of_an_adapter_that_wasnt_found_says_so()
    {
        _backup.Remember("nic|" + Nic + "|*EEE", "1");
        DeviceAdvancedViewModel vm = Build();
        await vm.OpenAsync(Row(Nic, "Intel I225-V", "Net"));

        await vm.RevertAsync();

        Assert.Equal("Restored 0 settings. 1 setting couldn't be restored: the adapter wasn't found — reopen the panel and try again", vm.Message);
        Assert.Empty(_nics.Sets);
        Assert.Contains("nic|" + Nic + "|*EEE", _backup.Originals.Keys);
    }

    [Fact]
    public async Task Opening_the_same_device_twice_concurrently_does_not_duplicate_properties()
    {
        _nics.Next = Adapter();
        DeviceAdvancedViewModel vm = Build();
        DeviceRowViewModel row = Row(Nic, "Intel I225-V", "Net");

        Task first = vm.OpenAsync(row);
        Task second = vm.OpenAsync(row);
        await Task.WhenAll(first, second);

        Assert.Equal(Adapter().Properties.Count, vm.NicProperties.Count);
    }

    private sealed class GatedNicPropertySource : INicPropertySource
    {
        private readonly ManualResetEventSlim _gate = new(false);
        public ManualResetEventSlim Entered { get; } = new(false);
        public NicInfo? Next { get; set; }
        public List<(string Adapter, string Keyword, string Value)> Sets { get; } = new();
        public string? SetResult { get; set; }

        public NicInfo? Read(string pnpDeviceId) => Next;

        public string? Set(string adapterName, string keyword, string value)
        {
            lock (Sets) Sets.Add((adapterName, keyword, value));
            Entered.Set();
            _gate.Wait(TimeSpan.FromSeconds(10));
            return SetResult;
        }

        public void Release() => _gate.Set();
    }
}
