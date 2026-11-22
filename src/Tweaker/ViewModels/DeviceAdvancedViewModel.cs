using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class NicPropertyViewModel : ObservableObject
{
    private readonly Func<NicPropertyViewModel, ChoiceOption, Task> _change;
    private ChoiceOption? _selected;

    public NicPropertyViewModel(NicProperty property, Func<NicPropertyViewModel, ChoiceOption, Task> change)
    {
        Keyword = property.Keyword;
        DisplayName = property.DisplayName;
        Options = property.Options;
        Hint = NicKeywords.HintFor(property.Keyword);
        _selected = Options.FirstOrDefault(o => o.Value == property.Value);
        _change = change;
    }

    public string Keyword { get; }
    public string DisplayName { get; }
    public string Hint { get; }
    public bool HasHint => Hint.Length > 0;
    public IReadOnlyList<ChoiceOption> Options { get; }
    public string CurrentValue => _selected?.Value ?? "";

    public ChoiceOption? Selected
    {
        get => _selected;
        set
        {
            if (value is null || ReferenceEquals(value, _selected)) return;
            _ = _change(this, value);
        }
    }

    internal void Commit(ChoiceOption? option)
    {
        if (option is null) return;
        _selected = option;
        OnPropertyChanged(nameof(Selected));
    }

    internal void SnapBack()
    {
        System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) OnPropertyChanged(nameof(Selected));
        else dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(Selected)));
    }
}

public sealed class DeviceAdvancedViewModel : ObservableObject
{
    private const string Busy = "Wait for the current change to finish.";
    private const string NoBackup = "Couldn't save a backup of the current value — nothing was changed.";
    private const string StorageNote = "Interrupt settings are hidden for storage controllers: a wrong setting can stop Windows from booting.";
    private static readonly string[] MsiLabels = { "Not set (line-based)", "On", "Off" };

    private readonly InterruptSettings _interrupts;
    private readonly INicPropertySource _nics;
    private readonly IAdvancedBackup _backup;
    private readonly int _processorCount;
    private string _id = "", _adapter = "", _deviceName = "", _message = "", _nicNote = "", _interruptNote = "";
    private bool _isOpen, _showInterrupts, _showNic, _isLoadingNic, _busy;
    private int _openGeneration;
    private MsiMode _msi;
    private AffinityChoice? _affinity;
    private IReadOnlyList<AffinityChoice> _affinityOptions = Array.Empty<AffinityChoice>();

    public DeviceAdvancedViewModel(InterruptSettings interrupts, INicPropertySource nics, IAdvancedBackup backup, int processorCount)
    {
        _interrupts = interrupts;
        _nics = nics;
        _backup = backup;
        _processorCount = processorCount;
        CloseCommand = new RelayCommand(_ => IsOpen = false);
        RevertCommand = new RelayCommand(async _ => await RevertAsync());
    }

    public bool IsOpen { get => _isOpen; private set => SetField(ref _isOpen, value); }
    public string DeviceName { get => _deviceName; private set => SetField(ref _deviceName, value); }
    public bool ShowInterrupts { get => _showInterrupts; private set => SetField(ref _showInterrupts, value); }
    public bool ShowNic { get => _showNic; private set => SetField(ref _showNic, value); }
    public bool IsLoadingNic { get => _isLoadingNic; private set => SetField(ref _isLoadingNic, value); }
    public string Message { get => _message; private set => SetField(ref _message, value); }

    public string NicNote
    {
        get => _nicNote;
        private set
        {
            if (SetField(ref _nicNote, value)) OnPropertyChanged(nameof(HasNicNote));
        }
    }

    public bool HasNicNote => _nicNote.Length > 0;

    public string InterruptNote
    {
        get => _interruptNote;
        private set
        {
            if (SetField(ref _interruptNote, value)) OnPropertyChanged(nameof(HasInterruptNote));
        }
    }

    public bool HasInterruptNote => _interruptNote.Length > 0;

    public IReadOnlyList<string> MsiOptions => MsiLabels;

    public string SelectedMsi
    {
        get => MsiLabels[(int)_msi];
        set => ChangeMsi(value);
    }

    public IReadOnlyList<AffinityChoice> AffinityOptions => _affinityOptions;

    public AffinityChoice? SelectedAffinity
    {
        get => _affinity;
        set { if (value is not null) ChangeAffinity(value); }
    }

    public ObservableCollection<NicPropertyViewModel> NicProperties { get; } = new();

    public bool CanRevert => _id.Length > 0 && _backup.Originals.Keys.Any(BelongsHere);
    public ICommand CloseCommand { get; }
    public ICommand RevertCommand { get; }

    public async Task OpenAsync(DeviceRowViewModel row)
    {
        if (_busy)
        {
            Message = Busy;
            IsOpen = true;
            return;
        }
        int generation = ++_openGeneration;

        _id = row.Key;
        _adapter = "";
        DeviceName = row.Name;
        Message = "";
        NicNote = "";
        NicProperties.Clear();
        IsLoadingNic = false;

        bool interruptCapable = InterruptRules.IsInterruptCapable(_id);
        bool storage = row.Group == "Storage";
        ShowInterrupts = interruptCapable && !storage;
        InterruptNote = interruptCapable && storage ? StorageNote : "";
        if (ShowInterrupts)
        {
            _msi = _interrupts.ReadMsi(_id);
            (int policy, ulong mask) = _interrupts.ReadAffinity(_id);
            SetAffinityState(policy, mask);
            OnPropertyChanged(nameof(SelectedMsi));
        }
        ShowNic = row.Group == "Network";
        IsOpen = true;
        OnPropertyChanged(nameof(CanRevert));
        if (!ShowNic) return;

        IsLoadingNic = true;
        string id = _id;
        NicInfo? info;
        try { info = await Task.Run(() => _nics.Read(id)); }
        catch { info = null; }
        if (generation != _openGeneration) return;
        IsLoadingNic = false;

        if (info is null)
        {
            NicNote = "Couldn't read this adapter's properties.";
            return;
        }
        _adapter = info.AdapterName;
        foreach (NicProperty property in info.Properties) NicProperties.Add(new NicPropertyViewModel(property, SetNicAsync));
        if (NicProperties.Count == 0) NicNote = "No tunable properties found for this adapter.";
    }

    public async Task SetNicAsync(NicPropertyViewModel property, ChoiceOption option)
    {
        if (_busy)
        {
            Message = Busy;
            property.SnapBack();
            return;
        }
        if (_adapter.Length == 0)
        {
            property.SnapBack();
            return;
        }
        string key = "nic|" + _id + "|" + property.Keyword;
        bool first = !_backup.Originals.ContainsKey(key);
        if (!_backup.Remember(key, property.CurrentValue))
        {
            Message = NoBackup;
            property.SnapBack();
            return;
        }
        _busy = true;
        Message = "Changing " + property.DisplayName + "…";

        string adapter = _adapter, keyword = property.Keyword, value = option.Value;
        string? error;
        try { error = await Task.Run(() => _nics.Set(adapter, keyword, value)); }
        catch (Exception ex) { error = ex.Message; }
        _busy = false;

        if (error is null)
        {
            property.Commit(option);
            ForgetIfOriginal(key, option.Value);
            Message = property.DisplayName + " set to " + option.Label + " — the adapter reconnects for a few seconds.";
        }
        else
        {
            if (first) _backup.Forget(key);
            property.SnapBack();
            Message = "Couldn't change " + property.DisplayName + ": " + error;
        }
        OnPropertyChanged(nameof(CanRevert));
    }

    public async Task RevertAsync()
    {
        if (_busy) return;
        if (_id.Length == 0) return;
        List<KeyValuePair<string, string>> mine = _backup.Originals.Where(kv => BelongsHere(kv.Key)).ToList();
        if (mine.Count == 0) return;

        string id = _id, adapter = _adapter;
        _busy = true;
        try
        {
            int done = 0;
            bool needsRestart = false;
            var failures = new List<string>();
            string nicPrefix = "nic|" + id + "|";
            foreach (KeyValuePair<string, string> original in mine)
            {
                string? error;
                if (string.Equals(original.Key, "msi|" + id, StringComparison.OrdinalIgnoreCase))
                {
                    MsiMode mode = InterruptRules.ParseMsi(original.Value);
                    error = _interrupts.WriteMsi(id, mode);
                    if (error is null && id == _id) _msi = mode;
                }
                else if (string.Equals(original.Key, "affinity|" + id, StringComparison.OrdinalIgnoreCase))
                {
                    (int policy, ulong mask) = InterruptRules.ParseAffinity(original.Value);
                    error = _interrupts.WriteAffinity(id, policy, mask);
                    if (error is null && id == _id) SetAffinityState(policy, mask);
                }
                else
                {
                    string keyword = original.Key[nicPrefix.Length..];
                    if (adapter.Length == 0)
                    {
                        error = "the adapter wasn't found — reopen the panel and try again";
                    }
                    else if (original.Value.Length == 0)
                    {
                        error = "the original value of " + keyword + " is unknown";
                    }
                    else
                    {
                        string value = original.Value;
                        try { error = await Task.Run(() => _nics.Set(adapter, keyword, value)); }
                        catch (Exception ex) { error = ex.Message; }
                        if (error is null && id == _id)
                        {
                            NicPropertyViewModel? property = NicProperties.FirstOrDefault(p => string.Equals(p.Keyword, keyword, StringComparison.OrdinalIgnoreCase));
                            property?.Commit(property.Options.FirstOrDefault(o => o.Value == value));
                        }
                    }
                }

                if (error is null)
                {
                    _backup.Forget(original.Key);
                    done++;
                    if (!original.Key.StartsWith("nic|", StringComparison.OrdinalIgnoreCase)) needsRestart = true;
                }
                else
                {
                    failures.Add(error);
                }
            }

            Message = "Restored " + Count(done) + "." + (needsRestart ? " Restart to apply." : "") + (failures.Count > 0 ? " " + Count(failures.Count) + " couldn't be restored: " + failures[0] : "");
            OnPropertyChanged(nameof(SelectedMsi));
            OnPropertyChanged(nameof(CanRevert));
        }
        finally
        {
            _busy = false;
        }
    }

    private void ChangeMsi(string label)
    {
        if (_busy)
        {
            Message = Busy;
            Renotify(nameof(SelectedMsi));
            return;
        }
        int index = Array.IndexOf(MsiLabels, label);
        if (index < 0 || _id.Length == 0 || (MsiMode)index == _msi) return;
        var next = (MsiMode)index;
        string key = "msi|" + _id;
        bool first = !_backup.Originals.ContainsKey(key);
        if (!_backup.Remember(key, InterruptRules.MsiText(_msi)))
        {
            Message = NoBackup;
            Renotify(nameof(SelectedMsi));
            return;
        }

        string? error = _interrupts.WriteMsi(_id, next);
        if (error is null)
        {
            _msi = next;
            ForgetIfOriginal(key, InterruptRules.MsiText(next));
            Message = "Message-signaled interrupts set to " + label + " — restart to apply.";
            OnPropertyChanged(nameof(SelectedMsi));
        }
        else
        {
            if (first) _backup.Forget(key);
            Message = "Couldn't change message-signaled interrupts: " + error;
            Renotify(nameof(SelectedMsi));
        }
        OnPropertyChanged(nameof(CanRevert));
    }

    private void ChangeAffinity(AffinityChoice choice)
    {
        if (_busy)
        {
            Message = Busy;
            Renotify(nameof(SelectedAffinity));
            return;
        }
        if (_id.Length == 0 || _affinity is null || choice == _affinity) return;
        string key = "affinity|" + _id;
        bool first = !_backup.Originals.ContainsKey(key);
        if (!_backup.Remember(key, InterruptRules.AffinityText(_affinity.Policy, _affinity.Mask)))
        {
            Message = NoBackup;
            Renotify(nameof(SelectedAffinity));
            return;
        }

        string? error = _interrupts.WriteAffinity(_id, choice.Policy, choice.Mask);
        if (error is null)
        {
            SetAffinityState(choice.Policy, choice.Mask);
            ForgetIfOriginal(key, InterruptRules.AffinityText(choice.Policy, choice.Mask));
            Message = "Interrupt affinity set to " + choice.Label + " — restart to apply.";
        }
        else
        {
            if (first) _backup.Forget(key);
            Renotify(nameof(SelectedAffinity));
            Message = "Couldn't change interrupt affinity: " + error;
        }
        OnPropertyChanged(nameof(CanRevert));
    }

    private void SetAffinityState(int policy, ulong mask)
    {
        List<AffinityChoice> options = InterruptRules.AffinityOptions(_processorCount).ToList();
        AffinityChoice current = InterruptRules.Match(options, policy, mask);
        if (!options.Contains(current)) options.Add(current);
        _affinityOptions = options;
        _affinity = current;
        OnPropertyChanged(nameof(AffinityOptions));
        OnPropertyChanged(nameof(SelectedAffinity));
    }

    private void Renotify(string name)
    {
        System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) OnPropertyChanged(name);
        else dispatcher.BeginInvoke(() => OnPropertyChanged(name));
    }

    private void ForgetIfOriginal(string key, string value)
    {
        if (_backup.Originals.TryGetValue(key, out string? original) && original == value) _backup.Forget(key);
    }

    private bool BelongsHere(string key) =>
        string.Equals(key, "msi|" + _id, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, "affinity|" + _id, StringComparison.OrdinalIgnoreCase)
        || key.StartsWith("nic|" + _id + "|", StringComparison.OrdinalIgnoreCase);

    private static string Count(int n) => n == 1 ? "1 setting" : n.ToString(CultureInfo.InvariantCulture) + " settings";
}
