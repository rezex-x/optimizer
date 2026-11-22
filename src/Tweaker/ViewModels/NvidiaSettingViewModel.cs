using System;
using System.Windows.Input;
using Tweaker.Core.Models;
using Tweaker.Core.Nvidia;
using Tweaker.Core.Services;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class NvidiaSettingViewModel : ObservableObject
{
    private readonly NvidiaSetting _setting;
    private readonly INvidiaSettingsService _service;
    private readonly NvidiaBackupStore _backup;
    private readonly IConfirmationService _confirm;
    private readonly Action<string> _log;
    private readonly PendingChangesViewModel? _pending;
    private uint? _current;
    private bool? _runningTarget;
    private bool? _stagedTarget;

    public NvidiaSettingViewModel(NvidiaSetting setting, INvidiaSettingsService service,
        NvidiaBackupStore backup, IConfirmationService confirm, Action<string> log, PendingChangesViewModel? pending = null)
    {
        _setting = setting; _service = service; _backup = backup; _confirm = confirm; _log = log; _pending = pending;
        ApplyCommand = new RelayCommand(_ => Apply());
        RevertCommand = new RelayCommand(_ => Revert());
        ToggleCommand = new RelayCommand(_ => Toggle());
        if (pending is not null)
            pending.Changed += (_, _) =>
            {
                if (!IsPending && _runningTarget is null) _stagedTarget = null;
                OnPropertyChanged(nameof(IsPending));
                OnPropertyChanged(nameof(IsSwitchOn));
                OnPropertyChanged(nameof(StatusText));
            };
    }

    public uint SettingId => _setting.SettingId;
    public string Name => _setting.Name;
    public string Category => string.IsNullOrWhiteSpace(_setting.Category) ? "Other" : _setting.Category;
    public string Description => _setting.Description;
    public string Benefit => _setting.Benefit;
    public string Downsides => _setting.Downsides;
    public RiskLevel Risk => _setting.Risk;
    public bool CanEdit => _service.IsAvailable;
    public bool IsApplied => _service.IsAvailable && _current == _setting.RecommendedValue;

    public string PendingKey => "nvidia|" + _setting.SettingId;

    public bool IsPending => _pending?.IsPending(PendingKey) == true;

    public bool IsSwitchOn => _runningTarget ?? (IsPending && _stagedTarget is { } t ? t : IsApplied);

    public ICommand ApplyCommand { get; }
    public ICommand RevertCommand { get; }
    public ICommand ToggleCommand { get; }

    public string StatusText => IsPending ? (_stagedTarget == false ? "will revert" : "will apply") : IsApplied ? "applied" : "";

    public void SetCurrent(uint? value)
    {
        _current = value;
        if (IsPending && _stagedTarget is { } target && IsApplied == target) _pending!.Withdraw(PendingKey);
        RaiseApplied();
        OnPropertyChanged(nameof(StatusText));
    }

    public bool StageApply()
    {
        if (_pending is null || !_service.IsAvailable || IsApplied || IsPending) return false;
        Stage("Apply", true, ApplyNow);
        return true;
    }

    private void Toggle()
    {
        if (_pending is null)
        {
            if (IsApplied) Revert(); else Apply();
            return;
        }
        if (IsPending) _pending.Unstage(PendingKey);
        else if (!_service.IsAvailable) return;
        else if (IsApplied) Stage("Revert", false, Revert);
        else Stage("Apply", true, ApplyNow);
    }

    private void Stage(string action, bool target, Func<bool> run)
    {
        _stagedTarget = target;
        _pending!.Stage(new PendingChange(
            PendingKey, _setting.Name, action, "NVIDIA", _setting.Risk, false,
            _setting.Description, _setting.Benefit, _setting.Downsides, _setting.Warning,
            () =>
            {
                if (!_service.IsAvailable)
                {
                    _log($"NVIDIA GPU not available: {_setting.Name}");
                    throw new PendingChangeFailedException();
                }
                if (_service.ReadSetting(_setting.SettingId) is { } live)
                {
                    SetCurrent(live);
                    if (IsApplied == target)
                    {
                        _log($"Skipped: {_setting.Name} ({(target ? "already applied" : "already off")})");
                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                }
                bool ok;
                _runningTarget = target;
                OnPropertyChanged(nameof(IsSwitchOn));
                try
                {
                    ok = run();
                }
                finally
                {
                    _runningTarget = null;
                    OnPropertyChanged(nameof(IsSwitchOn));
                }
                if (!ok) throw new PendingChangeFailedException();
                return System.Threading.Tasks.Task.CompletedTask;
            },
            () => OnPropertyChanged(nameof(IsSwitchOn))));
    }

    private void Apply()
    {
        if (!_service.IsAvailable) return;
        var request = new TweakConfirmRequest(_setting.Name, "NVIDIA", _setting.Risk, false,
            _setting.Description, _setting.Benefit, _setting.Downsides, _setting.Warning);
        if (!_confirm.Confirm(request)) { _log($"Cancelled: {_setting.Name}"); RaiseApplied(); return; }
        ApplyNow();
    }

    private bool ApplyNow()
    {
        if (!_service.IsAvailable) return false;
        try
        {
            uint? prior = _service.ApplySetting(_setting.SettingId, _setting.RecommendedValue);
            _backup.StorePriorIfAbsent(_setting.SettingId, prior);
            SetCurrent(_setting.RecommendedValue);
            _log($"Applied: {_setting.Name}");
            return true;
        }
        catch (NvidiaSettingsException ex) { _log($"ERROR applying {_setting.Name}: {ex.Message}"); RaiseApplied(); return false; }
    }

    private bool Revert()
    {
        if (!_service.IsAvailable) return false;
        try
        {
            uint target = _backup.GetPrior(_setting.SettingId) ?? _setting.DefaultValue;
            _service.ApplySetting(_setting.SettingId, target);
            SetCurrent(target);
            _log($"Reverted: {_setting.Name}");
            return true;
        }
        catch (NvidiaSettingsException ex) { _log($"ERROR reverting {_setting.Name}: {ex.Message}"); RaiseApplied(); return false; }
    }

    private void RaiseApplied()
    {
        OnPropertyChanged(nameof(IsApplied));
        OnPropertyChanged(nameof(IsSwitchOn));
    }
}
