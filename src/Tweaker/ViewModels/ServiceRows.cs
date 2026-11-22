using Tweaker.Controls;
using Tweaker.Core.Catalog;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class ServiceRowViewModel : ObservableObject
{
    private ServiceSample _sample;
    private bool _isConfirming, _isBusy, _isProfileManaged;

    public ServiceRowViewModel(ServiceSample sample)
    {
        _sample = sample;
        Name = sample.Name;
        IsProtected = ServiceRules.IsProtected(sample);
        Advice = ServiceSafety.For(sample.Name);
    }

    public string Key => Name;
    public string Name { get; }
    public string DisplayName => _sample.DisplayName;
    public string Description => _sample.Description;
    public string GroupTitle => _sample.IsWindows ? "WINDOWS" : "THIRD-PARTY";
    public int GroupOrder => _sample.IsWindows ? 1 : 0;

    public string StatusText => ServiceFormat.Status(_sample.State);
    public bool IsRunning => _sample.State == ServiceState.Running;
    public bool IsStopped => _sample.State == ServiceState.Stopped;
    public bool IsPending => _sample.State is ServiceState.StartPending or ServiceState.StopPending;
    public string StartupText => ServiceFormat.Startup(_sample.Startup);
    public bool IsDisabled => _sample.Startup == ServiceStartup.Disabled;
    public string PidText => ServiceFormat.Pid(_sample.State, _sample.Pid);

    public int Pid => _sample.Pid;

    public ServiceAdvice? Advice { get; }
    public bool HasAdvice => Advice is not null && !IsProtected;
    public string AdviceText => Advice?.Level switch
    {
        SafetyLevel.Safe => "SAFE TO STOP",
        SafetyLevel.Caution => "CAUTION",
        SafetyLevel.Keep => "KEEP RUNNING",
        _ => "",
    };
    public BadgeTone AdviceTone => Advice?.Level switch
    {
        SafetyLevel.Safe => BadgeTone.Low,
        SafetyLevel.Caution => BadgeTone.Medium,
        SafetyLevel.Keep => BadgeTone.High,
        _ => BadgeTone.Neutral,
    };
    public string AdviceReason => Advice?.Reason ?? "";

    public bool IsProtected { get; }
    public bool CanStop => _sample.AcceptsStop;

    public bool IsConfirming
    {
        get => _isConfirming;
        set { if (SetField(ref _isConfirming, value)) NotifyActions(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { if (SetField(ref _isBusy, value)) NotifyActions(); }
    }

    public bool IsProfileManaged
    {
        get => _isProfileManaged;
        set => SetField(ref _isProfileManaged, value);
    }

    public string LockText => IsProtected && IsRunning ? "protected" : IsRunning && !CanStop ? "can't stop" : "";
    public bool ShowLock => LockText.Length > 0;
    public bool ShowStop => IsRunning && !IsProtected && CanStop && !_isConfirming && !_isBusy;
    public bool ShowConfirm => _isConfirming && IsRunning && !_isBusy;
    public bool ShowStart => IsStopped && !_isBusy;
    public bool CanStart => !IsDisabled;

    public void Update(ServiceSample sample)
    {
        bool disarmed = _isConfirming && sample.State != ServiceState.Running;
        if (disarmed) _isConfirming = false;
        if (sample == _sample)
        {
            if (disarmed)
            {
                OnPropertyChanged(nameof(IsConfirming));
                NotifyActions();
            }
            return;
        }

        _sample = sample;
        if (disarmed) OnPropertyChanged(nameof(IsConfirming));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(StartupText));
        OnPropertyChanged(nameof(IsDisabled));
        OnPropertyChanged(nameof(PidText));
        OnPropertyChanged(nameof(Pid));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanStart));
        NotifyActions();
    }

    private void NotifyActions()
    {
        OnPropertyChanged(nameof(LockText));
        OnPropertyChanged(nameof(ShowLock));
        OnPropertyChanged(nameof(ShowStop));
        OnPropertyChanged(nameof(ShowConfirm));
        OnPropertyChanged(nameof(ShowStart));
    }
}
