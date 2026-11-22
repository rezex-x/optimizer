using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Tweaker.Core.Models;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class PowerValueViewModel : ObservableObject
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly Tweak _tweak;
    private readonly PowerSettingSpec _spec;
    private readonly IPowerSettings _power;
    private readonly IPowerBackup _backup;
    private readonly IConfirmationService _confirm;
    private readonly Action<string> _log;
    private readonly PendingChangesViewModel? _pending;
    private readonly Guid _subgroup, _setting;
    private readonly bool _validIds;
    private Guid? _scheme;
    private uint? _current, _default;
    private uint? _staged;
    private double _number;
    private string _message = "";

    public PowerValueViewModel(Tweak tweak, IPowerSettings power, IPowerBackup backup, IConfirmationService confirm, Action<string> log,
        PendingChangesViewModel? pending = null)
    {
        _tweak = tweak;
        _spec = tweak.Power ?? new PowerSettingSpec();
        _power = power;
        _backup = backup;
        _confirm = confirm;
        _log = log;
        _pending = pending;
        _validIds = Guid.TryParse(_spec.Subgroup, out _subgroup) & Guid.TryParse(_spec.Setting, out _setting);
        Options = _spec.Options.Select(o => new ChoiceOption(o.Value.ToString(Inv), o.Label)).ToList();
        ApplyCommand = new RelayCommand(_ => Apply());
        ResetCommand = new RelayCommand(_ => Reset());
        Refresh();
    }

    public bool IsChoice => _spec.Options.Count > 0;
    public bool IsSlider => !IsChoice;
    public double Min => _spec.Min;
    public double Max => _spec.Max;
    public double Step => Math.Max(1, _spec.Step);
    public string MinText => Format(_spec.Min);
    public string MaxText => Format(_spec.Max);
    public IReadOnlyList<ChoiceOption> Options { get; }

    public bool IsAvailable => _current is not null;

    public bool IsChanged => Key() is { } key && _backup.Originals.ContainsKey(key);

    public bool CanReset => IsAvailable && IsChanged;

    public double Number
    {
        get => _number;
        set
        {
            double snapped = Math.Clamp(Math.Round((value - Min) / Step) * Step + Min, Min, Max);
            if (!SetField(ref _number, snapped)) return;
            OnPropertyChanged(nameof(NumberText));
            if (_pending is not null) StageOrWithdraw((uint)snapped);
            OnPropertyChanged(nameof(HasPending));
            OnPropertyChanged(nameof(ShowApplyButton));
        }
    }

    public string NumberText
    {
        get => _number.ToString("0", Inv);
        set
        {
            if (double.TryParse(value, NumberStyles.Float, Inv, out double typed)) Number = typed;
            OnPropertyChanged(nameof(NumberText));
        }
    }

    public bool HasPending => IsSlider ? _current is { } current && (uint)_number != current : _staged is not null;

    public bool ShowApplyButton => _pending is null && HasPending;

    public ChoiceOption? SelectedOption
    {
        get => (_staged ?? _current) is { } shown ? Options.FirstOrDefault(o => o.Value == shown.ToString(Inv)) : null;
        set
        {
            if (value is null || _current is not { } current) return;
            if (!uint.TryParse(value.Value, NumberStyles.None, Inv, out uint picked)) return;
            if (_pending is not null)
            {
                StageOrWithdraw(picked);
                return;
            }
            if (picked == current) return;
            if (!Write(picked)) Renotify(nameof(SelectedOption));
        }
    }

    public string Message => _message;

    public string StatusLine
    {
        get
        {
            if (_current is not { } current) return "Not available on this PC";
            string line = "Plugged in: " + Describe(current);
            if (_default is { } fallback) line += " · Windows default " + Describe(fallback);
            return _message.Length > 0 ? line + " · " + _message : line;
        }
    }

    public ICommand ApplyCommand { get; }
    public ICommand ResetCommand { get; }

    public void Refresh()
    {
        _scheme = _validIds ? _power.ActiveScheme() : null;
        _current = _scheme is { } scheme ? _power.ReadAc(scheme, _subgroup, _setting) : null;
        _default = _scheme is { } plan && _current is not null ? _power.ReadAcDefault(plan, _subgroup, _setting) : null;
        _number = _current is { } current ? current : _spec.Min;
        if (_staged is { } staged && _pending?.IsPending(PendingKey) == true) _number = staged;
        else _staged = null;
        _message = "";
        foreach (string name in new[]
                 {
                     nameof(IsAvailable), nameof(IsChanged), nameof(CanReset), nameof(Number), nameof(NumberText),
                     nameof(HasPending), nameof(ShowApplyButton), nameof(SelectedOption), nameof(Message), nameof(StatusLine),
                 })
            OnPropertyChanged(name);
    }

    private string PendingKey => "power|" + _tweak.Id;

    private void Apply()
    {
        if (ShowApplyButton) Write((uint)_number);
    }

    private void StageOrWithdraw(uint value)
    {
        if (_pending is null || _current is not { } current) return;
        if (value == current)
        {
            if (_staged is null) return;
            _staged = null;
            _pending.Withdraw(PendingKey);
        }
        else
        {
            _staged = value;
            Guid? stagedOn = _scheme;
            Stage("Set to " + Describe(value), () => WriteQueued(value, stagedOn));
        }
        OnPropertyChanged(nameof(SelectedOption));
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(ShowApplyButton));
    }

    private void Stage(string action, Func<Task> execute) => _pending!.Stage(new PendingChange(
        PendingKey, _tweak.Name, action, _tweak.Category, _tweak.Risk, _tweak.RequiresRestart,
        _tweak.Description, _tweak.Benefit, _tweak.Downsides, _tweak.Warning, execute, Discard));

    private Task WriteQueued(uint value, Guid? stagedOn)
    {
        _staged = null;
        bool written = false;
        try
        {
            written = !PlanMovedSince(stagedOn) && Write(value, ask: false);
        }
        finally
        {
            if (!written) SnapBack();
        }
        if (!written) throw new PendingChangeFailedException();
        return Task.CompletedTask;
    }

    private bool PlanMovedSince(Guid? stagedOn)
    {
        if ((_validIds ? _power.ActiveScheme() : null) == stagedOn) return false;
        Refresh();
        SetMessage("The power plan changed — check the new value and try again.");
        return true;
    }

    private void Discard()
    {
        _staged = null;
        SnapBack();
    }

    private void SnapBack()
    {
        _number = _current is { } current ? current : _spec.Min;
        foreach (string name in new[]
                 {
                     nameof(Number), nameof(NumberText), nameof(SelectedOption), nameof(HasPending), nameof(ShowApplyButton),
                 })
            OnPropertyChanged(name);
    }

    private bool Write(uint value, bool ask = true)
    {
        if (_scheme is not { } scheme || _current is not { } current) return false;
        if (IsStale()) return false;
        if (ask)
        {
            if (!_confirm.Confirm(new TweakConfirmRequest(_tweak.Name, _tweak.Category, _tweak.Risk, _tweak.RequiresRestart,
                    _tweak.Description, _tweak.Benefit, _tweak.Downsides, _tweak.Warning)))
            {
                _log($"Cancelled: {_tweak.Name}");
                SetMessage("Cancelled — nothing changed.");
                return false;
            }
            if (IsStale()) return false;
        }

        string key = PowerKeys.Key(scheme, _subgroup, _setting);
        bool first = !_backup.Originals.ContainsKey(key);
        if (!_backup.Remember(key, current))
        {
            SetMessage("Couldn't save a backup of the current value — nothing was changed.");
            return false;
        }

        string? error = _power.WriteAc(scheme, _subgroup, _setting, value);
        if (error is not null)
        {
            if (_power.ReadAc(scheme, _subgroup, _setting) == value)
            {
                ForgetIfOriginal(key, value);
                _log($"Set {_tweak.Name} to {Describe(value)}, but re-applying the plan failed: {error}");
                Refresh();
                SetMessage("Saved, but Windows couldn't re-apply the plan (" + error + ") — it takes effect the next time the plan is activated.");
                return true;
            }

            if (first) _backup.Forget(key);
            _log($"ERROR changing {_tweak.Name}: {error}");
            SetMessage("Couldn't change it: " + error);
            return false;
        }

        ForgetIfOriginal(key, value);
        _log($"Set {_tweak.Name} to {Describe(value)}");
        Refresh();
        return true;
    }

    private void ForgetIfOriginal(string key, uint value)
    {
        if (_backup.Originals.TryGetValue(key, out uint original) && original == value) _backup.Forget(key);
    }

    private bool IsStale()
    {
        Guid? live = _validIds ? _power.ActiveScheme() : null;
        string? message = null;
        if (live != _scheme)
            message = "The power plan changed — check the new value and try again.";
        else if (live is { } scheme && _power.ReadAc(scheme, _subgroup, _setting) != _current)
            message = "The value changed outside this row — check it and try again.";
        if (message is null) return false;

        Refresh();
        SetMessage(message);
        return true;
    }

    private void Reset()
    {
        if (_pending is null)
        {
            ResetNow();
            return;
        }
        if (Key() is not { } key || !_backup.Originals.TryGetValue(key, out uint original)) return;
        _staged = null;
        Guid? stagedOn = _scheme;
        Stage("Reset to " + Describe(original), () =>
        {
            if (PlanMovedSince(stagedOn) || !ResetNow()) throw new PendingChangeFailedException();
            return Task.CompletedTask;
        });
        SnapBack();
    }

    private bool ResetNow()
    {
        if (_scheme is not { } scheme || Key() is not { } key || !_backup.Originals.TryGetValue(key, out uint original)) return false;
        if (IsStale()) return false;
        string? error = _power.WriteAc(scheme, _subgroup, _setting, original);
        if (error is not null)
        {
            _log($"ERROR resetting {_tweak.Name}: {error}");
            bool landed = _power.ReadAc(scheme, _subgroup, _setting) == original;
            if (landed) _backup.Forget(key);
            Refresh();
            SetMessage(landed
                ? "Restored " + Describe(original) + ", but Windows couldn't re-apply the plan (" + error + ") — it takes effect the next time the plan is activated."
                : "Couldn't reset it: " + error);
            return landed;
        }
        _backup.Forget(key);
        _log($"Reset {_tweak.Name} to {Describe(original)}");
        Refresh();
        SetMessage("Restored " + Describe(original) + ".");
        return true;
    }

    private string? Key() => _scheme is { } scheme ? PowerKeys.Key(scheme, _subgroup, _setting) : null;

    private string Describe(uint value) => IsChoice
        ? Options.FirstOrDefault(o => o.Value == value.ToString(Inv))?.Label ?? "custom (" + value.ToString(Inv) + ")"
        : Format(value);

    private string Format(long value) => value.ToString(Inv) + (_spec.Unit.Length > 0 ? " " + _spec.Unit : "");

    private void SetMessage(string message)
    {
        _message = message;
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(StatusLine));
    }

    private void Renotify(string name)
    {
        System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) OnPropertyChanged(name);
        else dispatcher.BeginInvoke(() => OnPropertyChanged(name));
    }
}
