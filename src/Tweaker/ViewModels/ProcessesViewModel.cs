using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public enum ProcessView { All, Apps, Background, Windows }

public sealed class ProcessesViewModel : ObservableObject, IDisposable
{
    private const double StaleSeconds = 5;
    private static readonly TimeSpan ConfirmDelay = TimeSpan.FromMilliseconds(500);

    private readonly IProcessSource _source;
    private readonly IGpuUsageSource _gpu;
    private readonly int _selfPid;
    private readonly string _windowsDir;
    private readonly Func<DateTime> _now;
    private readonly IProfileState? _profiles;
    private readonly FreedResourcesViewModel? _freed;
    private DateTime _armedAt;
    private ProcessSnapshot? _previous;
    private List<ProcessData> _latest = new();
    private long _totalMemory;
    private bool _refreshing;
    private string _filter = "";
    private ProcessView _view = ProcessView.All;
    private bool _ramPercent;
    private string _sortColumn = "Cpu";
    private bool _sortAscending;
    private string _message = "";

    public ProcessesViewModel(IProcessSource source, IGpuUsageSource gpu, int selfPid, string windowsDir, Func<DateTime>? now = null,
        IProfileState? profiles = null, FreedResourcesViewModel? freed = null)
    {
        _source = source;
        _gpu = gpu;
        _selfPid = selfPid;
        _windowsDir = windowsDir;
        _now = now ?? (() => DateTime.UtcNow);
        _profiles = profiles;
        _freed = freed;
        if (profiles is not null)
        {
            profiles.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(ProfileBadgeText));
                OnPropertyChanged(nameof(ShowProfileBadge));
            };
        }
        SetViewCommand = new RelayCommand(p => { if (Enum.TryParse(p?.ToString(), out ProcessView view)) View = view; });
        SetRamUnitCommand = new RelayCommand(p => RamPercent = p?.ToString() == "%");
        EndTaskCommand = new RelayCommand(p => { if (p is ProcessRowViewModel row) BeginEndTask(row); });
        ConfirmEndTaskCommand = new RelayCommand(async p => { if (p is ProcessRowViewModel row) await ConfirmEndTaskAsync(row); });
    }

    public ObservableCollection<ProcessRowViewModel> Rows { get; } = new();
    public string CountText => Rows.Count == 1 ? "1 process" : FormattableString.Invariant($"{Rows.Count} processes");

    public string ProfileBadgeText => _profiles?.BadgeText ?? "";
    public bool ShowProfileBadge => _profiles?.IsActive == true;

    public string Filter
    {
        get => _filter;
        set { if (SetField(ref _filter, value ?? "")) ApplyRows(); }
    }

    public ProcessView View
    {
        get => _view;
        private set
        {
            if (!SetField(ref _view, value)) return;
            OnPropertyChanged(nameof(IsAll));
            OnPropertyChanged(nameof(IsApps));
            OnPropertyChanged(nameof(IsBackground));
            OnPropertyChanged(nameof(IsWindows));
            ApplyRows();
        }
    }

    public bool IsAll => _view == ProcessView.All;
    public bool IsApps => _view == ProcessView.Apps;
    public bool IsBackground => _view == ProcessView.Background;
    public bool IsWindows => _view == ProcessView.Windows;
    public ICommand SetViewCommand { get; }

    public bool RamPercent
    {
        get => _ramPercent;
        private set
        {
            if (!SetField(ref _ramPercent, value)) return;
            OnPropertyChanged(nameof(IsRamMb));
            OnPropertyChanged(nameof(IsRamPercent));
            foreach (ProcessRowViewModel row in Rows) row.SetRamUnit(value, _totalMemory);
        }
    }

    public bool IsRamMb => !_ramPercent;
    public bool IsRamPercent => _ramPercent;
    public ICommand SetRamUnitCommand { get; }

    public string SortColumn => _sortColumn;
    public bool SortAscending => _sortAscending;

    public string Message { get => _message; private set => SetField(ref _message, value); }
    public ICommand EndTaskCommand { get; }
    public ICommand ConfirmEndTaskCommand { get; }

    public void SortBy(string column)
    {
        if (string.IsNullOrEmpty(column)) return;
        if (column == _sortColumn) _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = column is "Name" or "Owner";
        }
        OnPropertyChanged(nameof(SortColumn));
        OnPropertyChanged(nameof(SortAscending));
        ApplyRows();
    }

    public async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            (ProcessSnapshot Snapshot, IReadOnlyDictionary<int, double> Gpu) read;
            try
            {
                read = await Task.Run(() => (_source.Read(), _gpu.Read()));
            }
            catch
            {
                return;
            }
            if (ReferenceEquals(read.Snapshot, ProcessSnapshot.Empty)) return;

            ProcessSnapshot now = read.Snapshot;
            if (_previous is not null && (now.At - _previous.At).TotalSeconds > StaleSeconds) _previous = null;
            IReadOnlyDictionary<(int Pid, long Created), ProcessRate>? rates =
                _previous is null ? null : ProcessRates.Compute(_previous, now);
            _previous = now;
            _totalMemory = now.TotalMemoryBytes;

            var latest = new List<ProcessData>(now.Processes.Count);
            foreach (ProcessSample p in now.Processes)
            {
                ProcessRate? rate = rates is not null && rates.TryGetValue((p.Pid, p.CreatedTicks), out ProcessRate r) ? r : null;
                double? gpu = read.Gpu.Count == 0 ? null : read.Gpu.TryGetValue(p.Pid, out double g) ? g : 0;
                latest.Add(new ProcessData(p, ProcessRules.Kind(p, _windowsDir), ProcessRules.IsProtected(p, _selfPid),
                    rate?.CpuPercent, rate?.IoBytesPerSec, gpu));
            }
            _latest = latest;
            ApplyRows();
        }
        finally
        {
            _refreshing = false;
        }
    }

    public async Task ConfirmEndTaskAsync(ProcessRowViewModel row)
    {
        if (row.IsProtected)
        {
            row.IsConfirming = false;
            Message = ProtectedMessage(row);
            return;
        }
        if (!row.IsConfirming || _now() - _armedAt < ConfirmDelay) return;
        row.IsConfirming = false;

        int pid = row.Pid;
        long created = row.Created;
        string key = row.Key;
        double mb = ProcessFormat.ToMegabytes(row.RamBytes);
        string? error;
        try { error = await Task.Run(() => _source.Kill(pid, created)); }
        catch (Exception ex) { error = ex.Message; }

        Message = error is null ? "Ended " + row.Name : "Couldn't end " + row.Name + ": " + error;
        if (error is null) _freed?.RecordStopped("process|" + key, mb);
        await RefreshAsync();
    }

    public void Dispose() => _gpu.Dispose();

    private void BeginEndTask(ProcessRowViewModel row)
    {
        if (row.IsProtected)
        {
            Message = ProtectedMessage(row);
            return;
        }
        foreach (ProcessRowViewModel other in Rows)
        {
            if (!ReferenceEquals(other, row)) other.IsConfirming = false;
        }
        row.IsConfirming = true;
        _armedAt = _now();
        Message = "End " + row.Name + "? Click Confirm to end it.";
    }

    private string ProtectedMessage(ProcessRowViewModel row) =>
        row.Pid == _selfPid
            ? "Tweaker can't end itself."
            : row.Name + " is protected — Windows, security software or an anti-cheat needs it.";

    private void ApplyRows()
    {
        string filter = _filter.Trim();
        List<ProcessData> visible = _latest
            .Where(d => _view switch
            {
                ProcessView.Apps => d.Kind == ProcessKind.App,
                ProcessView.Background => d.Kind == ProcessKind.Background,
                ProcessView.Windows => d.Kind == ProcessKind.Windows,
                _ => true,
            })
            .Where(d => filter.Length == 0
                || d.Sample.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || d.Sample.Pid.ToString(CultureInfo.InvariantCulture) == filter)
            .ToList();
        if (Rows.Any(r => r.IsConfirming))
        {
            var position = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Rows.Count; i++) position[Rows[i].Key] = i;
            visible = visible.OrderBy(d => position.TryGetValue(d.Key, out int i) ? i : int.MaxValue).ToList();
        }
        else
        {
            visible.Sort(Compare);
        }

        KeyedSync.Apply(Rows, visible, d => d.Key, r => r.Key,
            d => new ProcessRowViewModel(d, _ramPercent, _totalMemory),
            (r, d) => r.Update(d, _ramPercent, _totalMemory));
        OnPropertyChanged(nameof(CountText));
    }

    private int Compare(ProcessData a, ProcessData b)
    {
        int c = _sortColumn switch
        {
            "Name" => string.Compare(a.Sample.Name, b.Sample.Name, StringComparison.OrdinalIgnoreCase),
            "Pid" => a.Sample.Pid.CompareTo(b.Sample.Pid),
            "RamBytes" => a.Sample.WorkingSetBytes.CompareTo(b.Sample.WorkingSetBytes),
            "Disk" => Shown(a.IoBytesPerSec is double da ? ProcessFormat.ToMegabytes(da) : null)
                .CompareTo(Shown(b.IoBytesPerSec is double db ? ProcessFormat.ToMegabytes(db) : null)),
            "Gpu" => Shown(a.Gpu).CompareTo(Shown(b.Gpu)),
            "Owner" => string.Compare(a.Sample.Owner, b.Sample.Owner, StringComparison.OrdinalIgnoreCase),
            _ => Shown(a.Cpu).CompareTo(Shown(b.Cpu)),
        };
        if (!_sortAscending) c = -c;
        if (c != 0) return c;
        c = string.Compare(a.Sample.Name, b.Sample.Name, StringComparison.OrdinalIgnoreCase);
        return c != 0 ? c : a.Sample.Pid.CompareTo(b.Sample.Pid);
    }

    private static double Shown(double? value) => value is double v ? Math.Round(v, 1) : -1;
}
