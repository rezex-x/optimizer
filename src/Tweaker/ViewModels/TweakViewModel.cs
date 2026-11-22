using System.Threading.Tasks;
using System.Windows.Input;
using Tweaker.Core.Models;
using Tweaker.Core.Services;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class TweakViewModel : ObservableObject
{
    private readonly Tweak _tweak;
    private readonly ITweakEngine _engine;
    private readonly IConfirmationService _confirm;
    private readonly Action<string> _log;
    private readonly Action? _changed;
    private readonly PendingChangesViewModel? _pending;
    private TweakState _state;
    private string? _busyText;
    private bool? _runningTarget;
    private bool? _stagedTarget;

    public TweakViewModel(Tweak tweak, ITweakEngine engine, IConfirmationService confirm, Action<string> log,
        PowerValueViewModel? value = null, Action? changed = null, PendingChangesViewModel? pending = null,
        AppRemovalViewModel? apps = null)
    {
        _tweak = tweak;
        _engine = engine;
        _confirm = confirm;
        _log = log;
        _changed = changed;
        _pending = pending;
        Value = value;
        Apps = apps;
        _state = tweak.Type == TweakType.Value ? TweakState.Unknown : engine.GetState(tweak);
        if (value is not null)
            value.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PowerValueViewModel.IsChanged)) RaiseApplied();
            };
        if (pending is not null)
            pending.Changed += (_, _) =>
            {
                if (!IsPending && _runningTarget is null) _stagedTarget = null;
                OnPropertyChanged(nameof(IsPending));
                OnPropertyChanged(nameof(IsSwitchOn));
                OnPropertyChanged(nameof(StatusText));
            };
        if (apps is not null)
            apps.SelectionChanged += (_, _) => OnAppsSelectionChanged();
        ToggleCommand = new RelayCommand(async _ => await ToggleAsync());
        RunCommand = new RelayCommand(async _ => await RunAsync());
    }

    public string Id => _tweak.Id;
    public string Name => _tweak.Name;
    public string Description => _tweak.Description;
    public RiskLevel Risk => _tweak.Risk;
    public bool RequiresRestart => _tweak.RequiresRestart;
    public bool IsBatch => _tweak.Type == TweakType.Batch;
    public bool IsRegistry => _tweak.Type == TweakType.Registry;
    public bool IsRunOnce => _tweak.RunOnce;

    public bool IsValue => _tweak.Type == TweakType.Value;
    public PowerValueViewModel? Value { get; }

    public AppRemovalViewModel? Apps { get; }
    public bool HasApps => Apps is not null;

    public bool ShowSwitch => !_tweak.RunOnce && !IsValue;

    public TweakState State
    {
        get => _state;
        private set
        {
            if (!SetField(ref _state, value)) return;
            RaiseApplied();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool IsApplied => IsValue ? Value?.IsChanged == true : _state == TweakState.Applied;
    public bool IsBusy => _busyText is not null;
    public bool CanToggle => _busyText is null;

    public bool CanRun => _busyText is null && (Apps is null || Apps.SelectedCount > 0);

    public string PendingKey => "tweak|" + _tweak.Id;

    public bool IsPending => _pending?.IsPending(PendingKey) == true;

    public bool IsSwitchOn => _runningTarget ?? (IsPending && _stagedTarget is { } t ? t : IsApplied);

    public string StatusText => _busyText ?? (IsPending
        ? (_stagedTarget switch { true => "will apply", false => "will revert", null => "will run" })
        : IsApplied && !_tweak.RunOnce && !IsValue ? "applied" : "");

    public ICommand ToggleCommand { get; }
    public ICommand RunCommand { get; }

    public Task ToggleAsync()
    {
        if (IsValue) return Task.CompletedTask;
        if (_pending is null) return IsApplied ? RevertAsync() : ApplyAsync();
        if (IsPending) _pending.Unstage(PendingKey);
        else if (IsBusy) RaiseApplied();
        else if (IsApplied) Stage("Revert", false, RevertNowAsync);
        else Stage("Apply", true, ApplyNowAsync);
        return Task.CompletedTask;
    }

    public bool StageApply()
    {
        if (_pending is null || IsValue || IsBusy || IsPending || IsApplied) return false;
        if (NoAppsChosen()) return false;
        if (_tweak.RunOnce) Stage("Run", true, RunOnceNowAsync);
        else Stage("Apply", true, ApplyNowAsync);
        return true;
    }

    public void RefreshState()
    {
        if (IsBusy) return;
        if (IsValue)
        {
            Value?.Refresh();
            RaiseApplied();
            return;
        }
        State = _engine.GetState(_tweak);
        if (IsPending && _stagedTarget is { } target && AtTarget(_state, target)) _pending!.Withdraw(PendingKey);
        RaiseApplied();
    }

    public async Task ApplyAsync()
    {
        if (IsBusy || IsValue) return;
        if (!Confirmed())
        {
            _log($"Cancelled: {_tweak.Name}");
            RaiseApplied();
            return;
        }
        await ApplyNowAsync();
    }

    public async Task RevertAsync()
    {
        if (IsBusy || IsValue) return;
        await RevertNowAsync();
    }

    public async Task RunAsync()
    {
        if (IsBusy || IsValue) return;
        if (NoAppsChosen()) return;
        if (_pending is not null)
        {
            Stage("Run", true, RunOnceNowAsync);
            return;
        }
        if (!Confirmed())
        {
            _log($"Cancelled: {_tweak.Name}");
            return;
        }
        await RunOnceNowAsync();
    }

    private bool NoAppsChosen()
    {
        if (Apps is null || Apps.SelectedCount > 0) return false;
        _log("Choose at least one app to remove");
        return true;
    }

    private void OnAppsSelectionChanged()
    {
        OnPropertyChanged(nameof(CanRun));
        if (_pending is null || !IsPending) return;
        if (Apps!.SelectedCount == 0) _pending.Unstage(PendingKey);
        else Stage("Run", true, RunOnceNowAsync);
    }

    private Task<bool> RunOnceNowAsync() => Apps is null ? RunNowAsync() : RemoveAppsNowAsync();

    private async Task<bool> RemoveAppsNowAsync()
    {
        AppRemovalViewModel apps = Apps!;
        SetBusy("running…");
        try
        {
            await apps.RemoveSelectedAsync();
            return apps.LastFailedCount == 0;
        }
        catch (Exception ex)
        {
            _log($"ERROR running {_tweak.Name}: {ex.Message}");
            return false;
        }
        finally
        {
            SetBusy(null);
            RaiseApplied();
            _changed?.Invoke();
        }
    }

    private Task<bool> ApplyNowAsync() =>
        RunEngineAsync("applying…", () => _engine.Apply(_tweak), $"Applied: {_tweak.Name}", $"ERROR applying {_tweak.Name}");

    private Task<bool> RevertNowAsync() =>
        RunEngineAsync("reverting…", () => _engine.Revert(_tweak), $"Reverted: {_tweak.Name}", $"ERROR reverting {_tweak.Name}");

    private Task<bool> RunNowAsync() =>
        RunEngineAsync("running…", () => _engine.Apply(_tweak), $"Ran: {_tweak.Name}", $"ERROR running {_tweak.Name}");

    private void Stage(string action, bool target, Func<Task<bool>> run)
    {
        _stagedTarget = _tweak.RunOnce ? null : target;
        string name = _tweak.Name, description = _tweak.Description;
        if (Apps is not null)
        {
            IReadOnlyList<RemovableAppRowViewModel> chosen = Apps.Selected;
            name += " (" + chosen.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + (chosen.Count == 1 ? " app)" : " apps)");
            description = "Removes: " + string.Join(", ", chosen.Select(a => a.Name));
        }
        _pending!.Stage(new PendingChange(
            PendingKey, name, action, _tweak.Category, _tweak.Risk, _tweak.RequiresRestart,
            description, _tweak.Benefit, _tweak.Downsides, _tweak.Warning,
            async () =>
            {
                if (IsBusy)
                {
                    _log($"Skipped: {_tweak.Name} (still running)");
                    throw new PendingChangeFailedException();
                }
                if (!_tweak.RunOnce)
                {
                    TweakState live = _engine.GetState(_tweak);
                    if (AtTarget(live, target))
                    {
                        _log($"Skipped: {_tweak.Name} ({(target ? "already applied" : "already off")})");
                        State = live;
                        RaiseApplied();
                        OnPropertyChanged(nameof(StatusText));
                        return;
                    }
                }
                bool ok;
                _runningTarget = target;
                OnPropertyChanged(nameof(IsSwitchOn));
                try
                {
                    ok = await run();
                }
                finally
                {
                    _runningTarget = null;
                    OnPropertyChanged(nameof(IsSwitchOn));
                }
                if (!ok) throw new PendingChangeFailedException();
            },
            () => OnPropertyChanged(nameof(IsSwitchOn)),
            _tweak.RunOnce));
    }

    private static bool AtTarget(TweakState state, bool target) =>
        target ? state == TweakState.Applied : state == TweakState.NotApplied;

    private bool Confirmed() => _confirm.Confirm(new TweakConfirmRequest(
        _tweak.Name, _tweak.Category, _tweak.Risk, _tweak.RequiresRestart,
        _tweak.Description, _tweak.Benefit, _tweak.Downsides, _tweak.Warning));

    private async Task<bool> RunEngineAsync(string busyText, Action call, string okMessage, string errorPrefix)
    {
        SetBusy(busyText);
        try
        {
            await Task.Run(call);
            State = _engine.GetState(_tweak);
            _log(okMessage);
            return true;
        }
        catch (Exception ex)
        {
            _log($"{errorPrefix}: {ex.Message}");
            return false;
        }
        finally
        {
            SetBusy(null);
            RaiseApplied();
            _changed?.Invoke();
        }
    }

    private void RaiseApplied()
    {
        OnPropertyChanged(nameof(IsApplied));
        OnPropertyChanged(nameof(IsSwitchOn));
    }

    private void SetBusy(string? text)
    {
        _busyText = text;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanToggle));
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(StatusText));
    }
}
