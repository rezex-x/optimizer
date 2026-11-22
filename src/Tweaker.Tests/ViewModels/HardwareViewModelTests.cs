using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class HardwareViewModelTests
{
    private DateTime _clock = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    private readonly FakeDeviceSource _devices = new();
    private readonly FakeDevicePowerBackup _backup = new();

    private HardwareViewModel Build() =>
        new(_devices, _backup, new StorageViewModel(new FakeStorageDiagnostic()), () => _clock);

    private static DeviceSample D(string id, string name, string cls, bool? power = null, int code = 0, string maker = "Maker") =>
        new(id, name, cls, maker, code, power);

    private static DeviceSnapshot Snap(params DeviceSample[] devices) => new(devices);

    private static DeviceSnapshot Pc() => Snap(
        D("GPU", "NVIDIA GeForce RTX 4060 Ti", "Display", maker: "NVIDIA"),
        D("HUB", "USB Root Hub (USB 3.0)", "USB", power: true, maker: "Microsoft"),
        D("XHCI", "AMD USB 3.10 eXtensible Host Controller", "USB", power: false, maker: "AMD"),
        D("BAD", "Unknown USB Device (Device Descriptor Request Failed)", "USB", code: 43, maker: ""),
        D("MOUSE", "Logitech G PRO X SUPERLIGHT", "HIDClass", power: true, maker: "Logitech"),
        D("NIC", "Intel(R) Ethernet Controller I225-V", "Net", power: true, maker: "Intel"),
        D("SW", "Microsoft Basic Render", "SoftwareDevice"),
        D("OFF", "Old Webcam", "Camera", code: 22));

    [Fact]
    public async Task Devices_group_in_class_order_with_counts_and_noise_hidden()
    {
        _devices.Next = Pc();
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();

        Assert.Equal(new[] { "DISPLAY (1)", "USB (3)", "INPUT (1)", "NETWORK (1)", "OTHER (1)" }, vm.Groups.Select(g => g.Title));
        Assert.Equal(new[] { "AMD USB 3.10 eXtensible Host Controller", "Unknown USB Device (Device Descriptor Request Failed)", "USB Root Hub (USB 3.0)" },
            vm.Groups[1].Rows.Select(r => r.Name));
        Assert.Equal("7 devices", vm.CountText);
        Assert.Equal(new[] { "All", "Display", "USB", "Input", "Network", "Storage", "Other" }, vm.Classes.Select(c => c.Name));
        Assert.Equal(7, vm.Classes[0].Count);
        Assert.True(vm.Classes[0].IsActive);
    }

    [Fact]
    public async Task Problem_and_disabled_devices_are_marked()
    {
        _devices.Next = Pc();
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();
        DeviceRowViewModel bad = Row(vm, "BAD"), off = Row(vm, "OFF"), hub = Row(vm, "HUB");

        Assert.True(bad.IsProblem);
        Assert.False(bad.IsOk);
        Assert.Equal("Code 43 · Windows has stopped this device", bad.Description);
        Assert.True(off.IsDisabled);
        Assert.False(off.IsProblem);
        Assert.Equal("Maker · Code 22 · Disabled", off.Description);
        Assert.True(hub.IsOk);
        Assert.Equal("Microsoft", hub.Description);
        Assert.True(hub.HasPowerSetting);
        Assert.True(hub.PowerSaving);
        Assert.False(Row(vm, "GPU").HasPowerSetting);
    }

    [Fact]
    public async Task The_class_list_filters_and_decides_when_drives_show()
    {
        _devices.Next = Pc();
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();
        Assert.True(vm.ShowStorage);

        vm.SelectClassCommand.Execute("USB");
        Assert.Equal("USB", vm.SelectedClass);
        Assert.Equal(new[] { "USB (3)" }, vm.Groups.Select(g => g.Title));
        Assert.False(vm.ShowStorage);
        Assert.True(vm.Classes.Single(c => c.Name == "USB").IsActive);
        Assert.False(vm.Classes[0].IsActive);

        vm.SelectClassCommand.Execute("Storage");
        Assert.True(vm.ShowStorage);
        Assert.Empty(vm.Groups);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task Search_and_the_power_filter_narrow_the_list()
    {
        _devices.Next = Pc();
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();

        vm.Filter = "logitech";
        Assert.Equal(new[] { "MOUSE" }, vm.Groups.SelectMany(g => g.Rows).Select(r => r.Key));
        vm.Filter = "microsoft";
        Assert.Equal(new[] { "HUB" }, vm.Groups.SelectMany(g => g.Rows).Select(r => r.Key));
        vm.Filter = "";

        vm.SetPowerFilterCommand.Execute("On");
        Assert.True(vm.IsPowerOn);
        Assert.Equal(new[] { "HUB", "MOUSE", "NIC" }, vm.Groups.SelectMany(g => g.Rows).Select(r => r.Key).OrderBy(k => k));
        vm.SetPowerFilterCommand.Execute("Off");
        Assert.Equal(new[] { "XHCI" }, vm.Groups.SelectMany(g => g.Rows).Select(r => r.Key));
        vm.SetPowerFilterCommand.Execute("All");
        Assert.Equal("7 devices", vm.CountText);
    }

    [Fact]
    public async Task Toggling_remembers_the_original_and_sets_the_device()
    {
        _devices.Next = Pc();
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();
        DeviceRowViewModel hub = Row(vm, "HUB");

        await vm.TogglePowerAsync(hub);

        Assert.Equal(new[] { ("HUB", false) }, _devices.Sets);
        Assert.False(hub.PowerSaving);
        Assert.True(_backup.Originals["HUB"]);
        Assert.Equal("Power saving off for USB Root Hub (USB 3.0)", vm.Message);
        Assert.True(vm.ShowRevert);
        Assert.Equal("Revert power changes (1)", vm.RevertText);

        await vm.TogglePowerAsync(hub);
        Assert.True(hub.PowerSaving);
        Assert.False(vm.ShowRevert);
    }

    [Fact]
    public async Task A_failed_toggle_keeps_the_switch_and_says_why()
    {
        _devices.Next = Pc();
        _devices.SetResult = "Access is denied.";
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();
        DeviceRowViewModel hub = Row(vm, "HUB");

        await vm.TogglePowerAsync(hub);

        Assert.True(hub.PowerSaving);
        Assert.False(hub.IsBusy);
        Assert.Equal("Couldn't change USB Root Hub (USB 3.0): Access is denied.", vm.Message);
        Assert.False(vm.ShowRevert);
    }

    [Fact]
    public async Task Turning_off_usb_and_input_power_saving_needs_a_separate_confirm()
    {
        _devices.Next = Pc();
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();

        vm.TurnOffCommand.Execute(null);
        Assert.True(vm.ShowConfirmTurnOff);
        Assert.False(vm.ShowTurnOff);
        Assert.Equal("Turn off power saving on 2 USB / input devices? Click Confirm.", vm.Message);

        _clock = _clock.AddMilliseconds(200);
        await vm.ConfirmTurnOffAsync();
        Assert.Empty(_devices.Sets);

        _clock = _clock.AddSeconds(1);
        await vm.ConfirmTurnOffAsync();

        Assert.Equal(new[] { "HUB", "MOUSE" }, _devices.Sets.Select(s => s.Id).OrderBy(k => k));
        Assert.All(_devices.Sets, s => Assert.False(s.Enabled));
        Assert.False(Row(vm, "HUB").PowerSaving);
        Assert.True(Row(vm, "NIC").PowerSaving);
        Assert.Equal("Turned off power saving on 2 devices.", vm.Message);
        Assert.True(vm.ShowTurnOff);
        Assert.Equal(2, _backup.Originals.Count);
    }

    [Fact]
    public async Task Turning_off_reports_failures_and_nothing_to_do()
    {
        _devices.Next = Pc();
        _devices.FailIds.Add("MOUSE");
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();

        vm.TurnOffCommand.Execute(null);
        _clock = _clock.AddSeconds(1);
        await vm.ConfirmTurnOffAsync();
        Assert.Equal("Turned off power saving on 1 device. 1 device couldn't change.", vm.Message);
        Assert.True(Row(vm, "MOUSE").PowerSaving);
        Assert.Equal(new[] { "HUB" }, _backup.Originals.Keys);

        _devices.FailIds.Clear();
        _devices.Next = Snap(D("XHCI", "Host", "USB", power: false));
        await vm.RefreshAsync();
        vm.TurnOffCommand.Execute(null);
        Assert.False(vm.ShowConfirmTurnOff);
        Assert.Equal("Power saving is already off on every USB and input device.", vm.Message);
    }

    [Fact]
    public async Task Revert_puts_originals_back_and_keeps_what_failed()
    {
        _devices.Next = Pc();
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();
        await vm.TogglePowerAsync(Row(vm, "HUB"));
        await vm.TogglePowerAsync(Row(vm, "XHCI"));
        _devices.Sets.Clear();
        _devices.FailIds.Add("XHCI");

        await vm.RevertAsync();

        Assert.Contains(("HUB", true), _devices.Sets);
        Assert.Contains(("XHCI", false), _devices.Sets);
        Assert.True(Row(vm, "HUB").PowerSaving);
        Assert.Equal(new[] { "XHCI" }, _backup.Originals.Keys);
        Assert.Equal("Restored 1 device. 1 device couldn't be restored (AMD USB 3.10 eXtensible Host Controller: Access is denied.).", vm.Message);
        Assert.Equal("Revert power changes (1)", vm.RevertText);
    }

    [Fact]
    public async Task Rows_are_reused_and_a_failed_read_keeps_them()
    {
        _devices.Next = Pc();
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();
        DeviceRowViewModel hub = Row(vm, "HUB");

        _devices.Next = Snap(D("HUB", "USB Root Hub (USB 3.0)", "USB", power: false, maker: "Microsoft"));
        await vm.RefreshAsync();
        Assert.Same(hub, Row(vm, "HUB"));
        Assert.False(hub.PowerSaving);
        Assert.Equal("1 device", vm.CountText);

        _devices.Next = DeviceSnapshot.Empty;
        await vm.RefreshAsync();
        Assert.Equal("1 device", vm.CountText);
    }

    [Fact]
    public async Task A_refresh_during_a_running_read_is_skipped()
    {
        var gated = new GatedDeviceSource();
        var vm = new HardwareViewModel(gated, _backup, new StorageViewModel(new FakeStorageDiagnostic()));

        Task first = vm.RefreshAsync();
        Assert.True(gated.Entered.Wait(TimeSpan.FromSeconds(5)));
        await vm.RefreshAsync();
        Assert.Equal(1, gated.Reads);

        gated.Release.Set();
        await first;
    }

    [Fact]
    public void A_new_view_model_is_not_empty()
    {
        HardwareViewModel vm = Build();
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task A_failed_first_read_says_so()
    {
        _devices.Next = DeviceSnapshot.Empty;
        HardwareViewModel vm = Build();

        await vm.RefreshAsync();

        Assert.Equal("Couldn't read devices.", vm.Message);

        _devices.Next = Pc();
        await vm.RefreshAsync();
        Assert.Equal("", vm.Message);
    }

    [Fact]
    public async Task Applying_the_same_filtered_view_reuses_the_existing_groups()
    {
        _devices.Next = Snap(
            D("HUB", "USB Root Hub (USB 3.0)", "USB", power: true, maker: "Microsoft"),
            D("MOUSE", "Logitech G PRO X SUPERLIGHT", "HIDClass", power: true, maker: "Logitech"));
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();
        DeviceGroupViewModel original = vm.Groups[0];

        vm.SetPowerFilterCommand.Execute("On");
        vm.SetPowerFilterCommand.Execute("All");

        Assert.Same(original, vm.Groups[0]);
    }

    [Fact]
    public async Task Confirming_the_batch_forgets_a_device_that_lands_back_at_its_original()
    {
        _devices.Next = Pc();
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();
        DeviceRowViewModel xhci = Row(vm, "XHCI");

        await vm.TogglePowerAsync(xhci);
        Assert.True(xhci.PowerSaving);
        Assert.True(_backup.Originals.ContainsKey("XHCI"));

        vm.TurnOffCommand.Execute(null);
        _clock = _clock.AddSeconds(1);
        await vm.ConfirmTurnOffAsync();

        Assert.False(_backup.Originals.ContainsKey("XHCI"));
    }

    [Fact]
    public async Task Refreshing_disarms_a_pending_turn_off()
    {
        _devices.Next = Pc();
        HardwareViewModel vm = Build();
        await vm.RefreshAsync();

        vm.TurnOffCommand.Execute(null);
        Assert.True(vm.ShowConfirmTurnOff);
        Assert.False(vm.ShowTurnOff);

        await vm.RefreshAsync();

        Assert.False(vm.ShowConfirmTurnOff);
        Assert.True(vm.ShowTurnOff);
        Assert.Equal("", vm.Message);
    }

    [Fact]
    public async Task Revert_skips_a_device_with_a_change_in_flight()
    {
        var gated = new GatedSetDeviceSource();
        var vm = new HardwareViewModel(gated, _backup, new StorageViewModel(new FakeStorageDiagnostic()), () => _clock);
        await vm.RefreshAsync();
        DeviceRowViewModel hub = Row(vm, "HUB");

        Task toggling = vm.TogglePowerAsync(hub);
        Assert.True(gated.Entered.Wait(TimeSpan.FromSeconds(5)));

        await vm.RevertAsync();

        Assert.Equal(1, gated.Sets.Count(s => s.Id == "HUB"));
        Assert.Contains("HUB", _backup.Originals.Keys);

        gated.Release();
        await toggling;
    }

    [Fact]
    public async Task Tunable_rows_open_the_advanced_panel()
    {
        _devices.Next = Snap(
            D(@"PCI\VEN_8086&DEV_15F3\6&2", "Intel(R) Ethernet Controller I225-V", "Net", power: true),
            D("HUB", "USB Root Hub (USB 3.0)", "USB", power: true));
        var advanced = new DeviceAdvancedViewModel(new InterruptSettings(new FakeRegistryService()), new FakeNicPropertySource(),
            new FakeAdvancedBackup(), 4);
        var vm = new HardwareViewModel(_devices, _backup, new StorageViewModel(new FakeStorageDiagnostic()), advanced: advanced);
        await vm.RefreshAsync();
        DeviceRowViewModel nic = Row(vm, @"PCI\VEN_8086&DEV_15F3\6&2");

        Assert.True(nic.HasAdvanced);
        Assert.False(Row(vm, "HUB").HasAdvanced);
        Assert.Same(advanced, vm.Advanced);

        await advanced.OpenAsync(nic);
        Assert.True(vm.Advanced!.IsOpen);
        Assert.NotNull(vm.OpenAdvancedCommand);
    }

    private static DeviceRowViewModel Row(HardwareViewModel vm, string id) =>
        vm.Groups.SelectMany(g => g.Rows).Single(r => r.Key == id);

    private sealed class GatedDeviceSource : IDeviceSource
    {
        private int _reads;
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public int Reads => Volatile.Read(ref _reads);

        public DeviceSnapshot Read()
        {
            Interlocked.Increment(ref _reads);
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(10));
            return DeviceSnapshot.Empty;
        }

        public string? SetPowerSaving(string instanceId, bool enabled) => null;
    }

    private sealed class GatedSetDeviceSource : IDeviceSource
    {
        private readonly ManualResetEventSlim _gate = new(false);
        public ManualResetEventSlim Entered { get; } = new(false);
        public List<(string Id, bool Enabled)> Sets { get; } = new();

        public DeviceSnapshot Read() => Pc();

        public string? SetPowerSaving(string instanceId, bool enabled)
        {
            lock (Sets) Sets.Add((instanceId, enabled));
            if (string.Equals(instanceId, "HUB", StringComparison.OrdinalIgnoreCase))
            {
                Entered.Set();
                _gate.Wait(TimeSpan.FromSeconds(10));
            }
            return null;
        }

        public void Release() => _gate.Set();
    }
}
