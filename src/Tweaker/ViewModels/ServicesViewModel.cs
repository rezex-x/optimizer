using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public enum ServiceGroupFilter { All, ThirdParty, Windows }

public enum ServiceStatusFilter { Any, Running, Stopped }

public sealed class ServicesViewModel : ObservableObject
{
    private static readonly TimeSpan ConfirmDelay = TimeSpan.FromMilliseconds(500);

    private readonly IServiceSource _source;
    private readonly Func<DateTime> _now;
    private readonly IProfileState? _profiles;
    private readonly FreedResourcesViewModel? _freed;
    private readonly Func<int, long?>? _workingSetOf;
    private List<ServiceSample> _latest = new();
    private readonly Dictionary<string, int> _recordedStops = new(StringComparer.OrdinalIgnoreCase);
    private int _readsStarted;
    private bool _refreshing;
    private string _filter = "";
    private ServiceGroupFilter _group = ServiceGroupFilter.All;
    private ServiceStatusFilter _status = ServiceStatusFilter.Any;
    private string _message = "";
    private DateTime _armedAt;

    public ServicesViewModel(IServiceSource source, Func<DateTime>? now = null, IProfileState? profiles = null,
        FreedResourcesViewModel? freed = null, Func<int, long?>? workingSetOf = null)
    {
        _source = source;
        _now = now ?? (() => DateTime.UtcNow);
        _profiles = profiles;
        _freed = freed;
        _workingSetOf = workingSetOf;
        if (profiles is not null) profiles.PropertyChanged += (_, _) => MarkProfileRows();
        SetGroupCommand = new RelayCommand(p => { if (Enum.TryParse(p?.ToString(), out ServiceGroupFilter g)) Group = g; });
        SetStatusCommand = new RelayCommand(p => { if (Enum.TryParse(p?.ToString(), out ServiceStatusFilter s)) Status = s; });
        StartCommand = new RelayCommand(async p => { if (p is ServiceRowViewModel row) await StartAsync(row); });
        StopCommand = new RelayCommand(p => { if (p is ServiceRowViewModel row) ArmStop(row); });
        ConfirmStopCommand = new RelayCommand(async p => { if (p is ServiceRowViewModel row) await ConfirmStopAsync(row); });
    }

    public ObservableCollection<ServiceRowViewModel> Rows { get; } = new();
    public string CountText => Rows.Count == 1 ? "1 service" : FormattableString.Invariant($"{Rows.Count} services");

    public string Filter
    {
        get => _filter;
        set { if (SetField(ref _filter, value ?? "")) ApplyRows(); }
    }

    public ServiceGroupFilter Group
    {
        get => _group;
        private set
        {
            if (!SetField(ref _group, value)) return;
            OnPropertyChanged(nameof(IsAllGroups));
            OnPropertyChanged(nameof(IsThirdPartyOnly));
            OnPropertyChanged(nameof(IsWindowsOnly));
            ApplyRows();
        }
    }

    public bool IsAllGroups => _group == ServiceGroupFilter.All;
    public bool IsThirdPartyOnly => _group == ServiceGroupFilter.ThirdParty;
    public bool IsWindowsOnly => _group == ServiceGroupFilter.Windows;
    public ICommand SetGroupCommand { get; }

    public ServiceStatusFilter Status
    {
        get => _status;
        private set
        {
            if (!SetField(ref _status, value)) return;
            OnPropertyChanged(nameof(IsAnyStatus));
            OnPropertyChanged(nameof(IsRunningOnly));
            OnPropertyChanged(nameof(IsStoppedOnly));
            ApplyRows();
        }
    }

    public bool IsAnyStatus => _status == ServiceStatusFilter.Any;
    public bool IsRunningOnly => _status == ServiceStatusFilter.Running;
    public bool IsStoppedOnly => _status == ServiceStatusFilter.Stopped;
    public ICommand SetStatusCommand { get; }

    public string Message { get => _message; private set => SetField(ref _message, value); }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ConfirmStopCommand { get; }

    public async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            int read = ++_readsStarted;
            ServiceSnapshot snapshot;
            try { snapshot = await Task.Run(() => _source.Read()); }
            catch { return; }
            if (ReferenceEquals(snapshot, ServiceSnapshot.Empty)) return;

            _latest = snapshot.Services.ToList();
            ApplyRows();
            ForgetRestarted(read);
        }
        finally
        {
            _refreshing = false;
        }
    }

    public async Task StartAsync(ServiceRowViewModel row)
    {
        if (row.IsBusy) return;
        if (row.IsDisabled)
        {
            Message = row.DisplayName + " is disabled — set its startup type first.";
            return;
        }

        row.IsBusy = true;
        Message = "Starting " + row.DisplayName + "…";
        string name = row.Name;
        string? error;
        try { error = await Task.Run(() => _source.Start(name)); }
        catch (Exception ex) { error = ex.Message; }
        row.IsBusy = false;
        Message = error is null ? "Started " + row.DisplayName : "Couldn't start " + row.DisplayName + ": " + error;
        if (error is null)
        {
            _freed?.Forget(FreedKey(name));
            _recordedStops.Remove(name);
        }
        await RefreshAsync();
    }

    public async Task ConfirmStopAsync(ServiceRowViewModel row)
    {
        if (row.IsProtected)
        {
            row.IsConfirming = false;
            Message = ProtectedMessage(row);
            return;
        }
        if (!row.IsConfirming || !row.IsRunning || row.IsBusy || _now() - _armedAt < ConfirmDelay) return;
        row.IsConfirming = false;

        row.IsBusy = true;
        Message = "Stopping " + row.DisplayName + "…";
        string name = row.Name;
        int pid = row.Pid;
        bool measure = _freed is not null && _workingSetOf is not null && pid > 0 && !SharesProcess(name, pid);
        long? bytes = null;
        string? error;
        try
        {
            error = await Task.Run(() =>
            {
                if (measure) bytes = WorkingSet(pid);
                return _source.Stop(name);
            });
        }
        catch (Exception ex) { error = ex.Message; }
        row.IsBusy = false;
        Message = error is null ? "Stopped " + row.DisplayName : "Couldn't stop " + row.DisplayName + ": " + error;
        if (error is null && bytes is long b)
        {
            _freed!.RecordStopped(FreedKey(name), b / 1048576.0);
            _recordedStops[name] = _readsStarted;
        }
        await RefreshAsync();
    }

    private void ArmStop(ServiceRowViewModel row)
    {
        if (row.IsProtected)
        {
            Message = ProtectedMessage(row);
            return;
        }
        if (!row.CanStop)
        {
            Message = row.DisplayName + " can't be stopped — Windows doesn't allow it.";
            return;
        }
        if (!row.IsRunning || row.IsBusy) return;
        foreach (ServiceRowViewModel other in Rows)
        {
            if (!ReferenceEquals(other, row)) other.IsConfirming = false;
        }
        row.IsConfirming = true;
        _armedAt = _now();
        Message = "Stop " + row.DisplayName + "? Click Confirm to stop it.";
    }

    private static string FreedKey(string name) => "service|" + name;

    private void ForgetRestarted(int read)
    {
        if (_recordedStops.Count == 0) return;
        foreach (ServiceSample sample in _latest)
        {
            if (sample.State is not (ServiceState.Running or ServiceState.StartPending)) continue;
            if (!_recordedStops.TryGetValue(sample.Name, out int recordedAt) || read <= recordedAt) continue;
            string recorded = _recordedStops.Keys.First(k => string.Equals(k, sample.Name, StringComparison.OrdinalIgnoreCase));
            _recordedStops.Remove(recorded);
            _freed?.Forget(FreedKey(recorded));
        }
    }

    private bool SharesProcess(string name, int pid) =>
        _latest.Any(s => s.State == ServiceState.Running && s.Pid == pid && !string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    private long? WorkingSet(int pid)
    {
        try { return _workingSetOf!(pid); }
        catch { return null; }
    }

    private static string ProtectedMessage(ServiceRowViewModel row) =>
        row.DisplayName + " is protected — Windows, security software or an anti-cheat needs it.";

    private void ApplyRows()
    {
        string filter = _filter.Trim();
        List<ServiceSample> visible = _latest
            .Where(s => _group switch
            {
                ServiceGroupFilter.ThirdParty => !s.IsWindows,
                ServiceGroupFilter.Windows => s.IsWindows,
                _ => true,
            })
            .Where(s => _status switch
            {
                ServiceStatusFilter.Running => s.State == ServiceState.Running,
                ServiceStatusFilter.Stopped => s.State == ServiceState.Stopped,
                _ => true,
            })
            .Where(s => filter.Length == 0
                || s.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || s.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.IsWindows)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        KeyedSync.Apply(Rows, visible, s => s.Name, r => r.Key, s => new ServiceRowViewModel(s), (r, s) => r.Update(s));
        MarkProfileRows();
        OnPropertyChanged(nameof(CountText));
    }

    private void MarkProfileRows()
    {
        foreach (ServiceRowViewModel row in Rows)
            row.IsProfileManaged = _profiles?.Manages(row.Name, row.DisplayName) == true;
    }
}
