using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public enum PowerFilter { All, On, Off }

public sealed class DeviceRowViewModel : ObservableObject
{
    private DeviceSample _sample;
    private bool? _powerSaving;
    private bool _isBusy;

    public DeviceRowViewModel(DeviceSample sample, string group)
    {
        _sample = sample;
        Group = group;
        _powerSaving = sample.PowerSaving;
    }

    public string Key => _sample.InstanceId;
    public string Group { get; private set; }
    public string Name => _sample.Name;
    public string Description => DeviceFormat.Description(_sample.Manufacturer, _sample.ErrorCode);
    public bool IsProblem => DeviceRules.IsProblem(_sample.ErrorCode);
    public bool IsDisabled => DeviceRules.IsDisabled(_sample.ErrorCode);
    public bool IsOk => !IsProblem;
    public bool HasPowerSetting => _powerSaving is not null;
    public bool PowerSaving => _powerSaving == true;
    public bool CanToggle => HasPowerSetting && !_isBusy;

    public bool HasAdvanced => InterruptRules.IsInterruptCapable(Key) || Group == "Network";

    public bool IsBusy
    {
        get => _isBusy;
        set { if (SetField(ref _isBusy, value)) OnPropertyChanged(nameof(CanToggle)); }
    }

    public void SetPower(bool? value)
    {
        _powerSaving = value;
        OnPropertyChanged(nameof(PowerSaving));
        OnPropertyChanged(nameof(HasPowerSetting));
        OnPropertyChanged(nameof(CanToggle));
    }

    public void Refresh() => OnPropertyChanged(nameof(PowerSaving));

    public void Update(DeviceSample sample, string group)
    {
        _sample = sample;
        Group = group;
        OnPropertyChanged(nameof(Group));
        OnPropertyChanged(nameof(HasAdvanced));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(IsProblem));
        OnPropertyChanged(nameof(IsDisabled));
        OnPropertyChanged(nameof(IsOk));
        if (!_isBusy) SetPower(sample.PowerSaving);
    }
}

public sealed class DeviceGroupViewModel
{
    public DeviceGroupViewModel(string name, IReadOnlyList<DeviceRowViewModel> rows)
    {
        Name = name;
        Rows = rows;
        Title = DeviceFormat.GroupTitle(name, rows.Count);
    }

    public string Name { get; }
    public string Title { get; }
    public IReadOnlyList<DeviceRowViewModel> Rows { get; }
}

public sealed class DeviceClassItem : ObservableObject
{
    private bool _isActive;

    public DeviceClassItem(string name, int count)
    {
        Name = name;
        Count = count;
    }

    public string Name { get; }
    public int Count { get; }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
}

public sealed class HardwareViewModel : ObservableObject
{
    private static readonly TimeSpan ConfirmDelay = TimeSpan.FromMilliseconds(500);

    private readonly IDeviceSource _devices;
    private readonly IDevicePowerBackup _backup;
    private readonly Func<DateTime> _now;
    private readonly Dictionary<string, DeviceRowViewModel> _rows = new(StringComparer.OrdinalIgnoreCase);
    private bool _refreshing, _reverting, _isTurnOffArmed, _loaded;
    private DateTime _armedAt;
    private string _class = "All";
    private string _filter = "";
    private PowerFilter _power = PowerFilter.All;
    private string _message = "";

    public HardwareViewModel(IDeviceSource devices, IDevicePowerBackup backup, StorageViewModel storage, Func<DateTime>? now = null,
        DeviceAdvancedViewModel? advanced = null)
    {
        _devices = devices;
        _backup = backup;
        Storage = storage;
        Advanced = advanced;
        _now = now ?? (() => DateTime.UtcNow);
        SelectClassCommand = new RelayCommand(p => SelectClass(p?.ToString()));
        SetPowerFilterCommand = new RelayCommand(p => { if (Enum.TryParse(p?.ToString(), out PowerFilter f)) Power = f; });
        TogglePowerCommand = new RelayCommand(async p => { if (p is DeviceRowViewModel row) await TogglePowerAsync(row); });
        TurnOffCommand = new RelayCommand(_ => ArmTurnOff());
        ConfirmTurnOffCommand = new RelayCommand(async _ => await ConfirmTurnOffAsync());
        RevertCommand = new RelayCommand(async _ => await RevertAsync());
        RefreshCommand = new RelayCommand(async _ => await Task.WhenAll(RefreshAsync(), Storage.RefreshAsync()));
        OpenAdvancedCommand = new RelayCommand(async p =>
        {
            if (p is DeviceRowViewModel row && Advanced is not null) await Advanced.OpenAsync(row);
        });
    }

    public StorageViewModel Storage { get; }

    public DeviceAdvancedViewModel? Advanced { get; }
    public ICommand OpenAdvancedCommand { get; }
    public ObservableCollection<DeviceClassItem> Classes { get; } = new();
    public ObservableCollection<DeviceGroupViewModel> Groups { get; } = new();

    public string SelectedClass => _class;
    public ICommand SelectClassCommand { get; }

    public string Filter
    {
        get => _filter;
        set
        {
            if (!SetField(ref _filter, value ?? "")) return;
            Disarm();
            ApplyView();
        }
    }

    public PowerFilter Power
    {
        get => _power;
        private set
        {
            if (!SetField(ref _power, value)) return;
            Disarm();
            OnPropertyChanged(nameof(IsPowerAll));
            OnPropertyChanged(nameof(IsPowerOn));
            OnPropertyChanged(nameof(IsPowerOff));
            ApplyView();
        }
    }

    public bool IsPowerAll => _power == PowerFilter.All;
    public bool IsPowerOn => _power == PowerFilter.On;
    public bool IsPowerOff => _power == PowerFilter.Off;
    public ICommand SetPowerFilterCommand { get; }

    public string CountText
    {
        get
        {
            int n = Groups.Sum(g => g.Rows.Count);
            return n == 1 ? "1 device" : n.ToString(CultureInfo.InvariantCulture) + " devices";
        }
    }

    public bool IsEmpty => _loaded && Groups.Count == 0;

    public bool ShowStorage => _class is "All" or "Storage";

    public string Message { get => _message; private set => SetField(ref _message, value); }

    public ICommand TogglePowerCommand { get; }
    public bool ShowTurnOff => !_isTurnOffArmed;
    public bool ShowConfirmTurnOff => _isTurnOffArmed;
    public ICommand TurnOffCommand { get; }
    public ICommand ConfirmTurnOffCommand { get; }

    public bool ShowRevert => _backup.Originals.Count > 0;
    public string RevertText => "Revert power changes (" + _backup.Originals.Count.ToString(CultureInfo.InvariantCulture) + ")";
    public ICommand RevertCommand { get; }
    public ICommand RefreshCommand { get; }

    public async Task RefreshAsync()
    {
        Disarm();
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            DeviceSnapshot snapshot;
            try { snapshot = await Task.Run(() => _devices.Read()); }
            catch { return; }
            if (ReferenceEquals(snapshot, DeviceSnapshot.Empty))
            {
                if (_rows.Count == 0) Message = "Couldn't read devices.";
                return;
            }
            _loaded = true;
            if (Message == "Couldn't read devices.") Message = "";

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DeviceSample d in snapshot.Devices)
            {
                string? group = DeviceRules.Group(d.PnpClass, d.ErrorCode);
                if (group is null) continue;
                seen.Add(d.InstanceId);
                if (_rows.TryGetValue(d.InstanceId, out DeviceRowViewModel? row)) row.Update(d, group);
                else _rows[d.InstanceId] = new DeviceRowViewModel(d, group);
            }
            foreach (string gone in _rows.Keys.Where(k => !seen.Contains(k)).ToList()) _rows.Remove(gone);

            BuildClasses();
            ApplyView();
        }
        finally
        {
            _refreshing = false;
        }
    }

    public async Task TogglePowerAsync(DeviceRowViewModel row)
    {
        if (!row.CanToggle) return;
        bool next = !row.PowerSaving;
        bool firstChange = !_backup.Originals.ContainsKey(row.Key);
        _backup.Remember(row.Key, row.PowerSaving);
        row.IsBusy = true;
        Message = (next ? "Turning on" : "Turning off") + " power saving for " + row.Name + "…";
        string key = row.Key;
        string? error = await Task.Run(() => Set(key, next));
        row.IsBusy = false;

        if (error is null)
        {
            row.SetPower(next);
            if (_backup.Originals.TryGetValue(key, out bool original) && original == next) _backup.Forget(key);
            Message = "Power saving " + (next ? "on" : "off") + " for " + row.Name;
        }
        else
        {
            if (firstChange) _backup.Forget(key);
            row.Refresh();
            Message = "Couldn't change " + row.Name + ": " + error;
        }
        RaiseBackup();
        if (_power != PowerFilter.All) ApplyView();
    }

    public async Task ConfirmTurnOffAsync()
    {
        if (!_isTurnOffArmed || _now() - _armedAt < ConfirmDelay) return;
        SetArmed(false);

        List<DeviceRowViewModel> targets = TurnOffTargets();
        if (targets.Count == 0) return;
        var firstChanges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DeviceRowViewModel row in targets)
        {
            if (!_backup.Originals.ContainsKey(row.Key)) firstChanges.Add(row.Key);
            _backup.Remember(row.Key, true);
            row.IsBusy = true;
        }
        Message = "Turning off power saving…";
        List<string> keys = targets.Select(r => r.Key).ToList();
        List<string?> errors = await Task.Run(() => keys.Select(k => Set(k, false)).ToList());

        IReadOnlyDictionary<string, bool> originalsSnapshot = _backup.Originals;
        int done = 0, failed = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].IsBusy = false;
            if (errors[i] is null)
            {
                targets[i].SetPower(false);
                if (originalsSnapshot.TryGetValue(targets[i].Key, out bool original) && original == false) _backup.Forget(targets[i].Key);
                done++;
            }
            else
            {
                if (firstChanges.Contains(targets[i].Key)) _backup.Forget(targets[i].Key);
                targets[i].Refresh();
                failed++;
            }
        }
        Message = "Turned off power saving on " + Count(done) + "." + (failed > 0 ? " " + Count(failed) + " couldn't change." : "");
        RaiseBackup();
        if (_power != PowerFilter.All) ApplyView();
    }

    public async Task RevertAsync()
    {
        if (_reverting) return;
        List<KeyValuePair<string, bool>> originals = _backup.Originals
            .Where(kv => !(_rows.TryGetValue(kv.Key, out DeviceRowViewModel? r) && r.IsBusy))
            .ToList();
        if (originals.Count == 0) return;

        _reverting = true;
        List<DeviceRowViewModel> affected = originals
            .Select(kv => _rows.TryGetValue(kv.Key, out DeviceRowViewModel? r) ? r : null)
            .OfType<DeviceRowViewModel>()
            .ToList();
        foreach (DeviceRowViewModel row in affected) row.IsBusy = true;
        try
        {
            Message = "Reverting power changes…";
            List<string?> errors = await Task.Run(() => originals.Select(kv => Set(kv.Key, kv.Value)).ToList());
            foreach (DeviceRowViewModel row in affected) row.IsBusy = false;

            int done = 0, failed = 0;
            string? firstFailureDetail = null;
            for (int i = 0; i < originals.Count; i++)
            {
                if (errors[i] is null)
                {
                    _backup.Forget(originals[i].Key);
                    if (_rows.TryGetValue(originals[i].Key, out DeviceRowViewModel? row)) row.SetPower(originals[i].Value);
                    done++;
                }
                else
                {
                    if (firstFailureDetail is null)
                    {
                        string name = _rows.TryGetValue(originals[i].Key, out DeviceRowViewModel? failedRow) ? failedRow.Name : originals[i].Key;
                        firstFailureDetail = name + ": " + errors[i];
                    }
                    failed++;
                }
            }
            Message = "Restored " + Count(done) + "."
                      + (failed > 0 ? " " + Count(failed) + " couldn't be restored (" + firstFailureDetail + ")." : "");
            RaiseBackup();
            if (_power != PowerFilter.All) ApplyView();
        }
        finally
        {
            _reverting = false;
        }
    }

    private void ArmTurnOff()
    {
        List<DeviceRowViewModel> targets = TurnOffTargets();
        if (targets.Count == 0)
        {
            Message = "Power saving is already off on every USB and input device.";
            return;
        }
        _armedAt = _now();
        SetArmed(true);
        Message = "Turn off power saving on " + targets.Count.ToString(CultureInfo.InvariantCulture)
                  + (targets.Count == 1 ? " USB / input device" : " USB / input devices") + "? Click Confirm.";
    }

    private List<DeviceRowViewModel> TurnOffTargets() => _rows.Values
        .Where(r => DeviceRules.IsUsbOrInput(r.Group) && r.HasPowerSetting && r.PowerSaving && !r.IsBusy)
        .ToList();

    private void SelectClass(string? name)
    {
        if (string.IsNullOrEmpty(name) || name == _class) return;
        Disarm();
        _class = name;
        foreach (DeviceClassItem item in Classes) item.IsActive = item.Name == _class;
        OnPropertyChanged(nameof(SelectedClass));
        OnPropertyChanged(nameof(ShowStorage));
        ApplyView();
    }

    private void BuildClasses()
    {
        Classes.Clear();
        Classes.Add(new DeviceClassItem("All", _rows.Count));
        foreach (string group in DeviceRules.GroupOrder)
        {
            int count = _rows.Values.Count(r => r.Group == group);
            if (count > 0 || group == "Storage") Classes.Add(new DeviceClassItem(group, count));
        }
        if (Classes.All(c => c.Name != _class))
        {
            _class = "All";
            OnPropertyChanged(nameof(SelectedClass));
            OnPropertyChanged(nameof(ShowStorage));
        }
        foreach (DeviceClassItem item in Classes) item.IsActive = item.Name == _class;
    }

    private void ApplyView()
    {
        string filter = _filter.Trim();
        IEnumerable<DeviceRowViewModel> rows = _rows.Values
            .Where(r => _class == "All" || r.Group == _class)
            .Where(r => filter.Length == 0
                || r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || r.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Where(r => _power switch
            {
                PowerFilter.On => r.HasPowerSetting && r.PowerSaving,
                PowerFilter.Off => r.HasPowerSetting && !r.PowerSaving,
                _ => true,
            });

        List<DeviceGroupViewModel> newGroups = rows
            .GroupBy(r => r.Group)
            .OrderBy(g => IndexOf(g.Key))
            .Select(g => new DeviceGroupViewModel(g.Key, g.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();

        if (!newGroups.SelectMany(g => g.Rows).SequenceEqual(Groups.SelectMany(g => g.Rows)))
        {
            Groups.Clear();
            foreach (DeviceGroupViewModel group in newGroups) Groups.Add(group);
        }
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private static int IndexOf(string group)
    {
        for (int i = 0; i < DeviceRules.GroupOrder.Count; i++)
        {
            if (DeviceRules.GroupOrder[i] == group) return i;
        }
        return DeviceRules.GroupOrder.Count;
    }

    private string? Set(string instanceId, bool enabled)
    {
        try { return _devices.SetPowerSaving(instanceId, enabled); }
        catch (Exception ex) { return ex.Message; }
    }

    private void Disarm()
    {
        if (!_isTurnOffArmed) return;
        SetArmed(false);
        Message = "";
    }

    private void SetArmed(bool armed)
    {
        _isTurnOffArmed = armed;
        OnPropertyChanged(nameof(ShowTurnOff));
        OnPropertyChanged(nameof(ShowConfirmTurnOff));
    }

    private void RaiseBackup()
    {
        OnPropertyChanged(nameof(ShowRevert));
        OnPropertyChanged(nameof(RevertText));
    }

    private static string Count(int n) => n == 1 ? "1 device" : n.ToString(CultureInfo.InvariantCulture) + " devices";
}
