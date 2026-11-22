using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class PerformanceViewModel : ObservableObject, IDisposable
{
    private readonly IHardwareMonitor _monitor;
    private readonly ISystemInfoSource _system;
    private readonly IVolumeSource? _volumes;
    private bool _refreshing;
    private bool _systemLoading;
    private bool _systemLoaded;
    private int? _cores;
    private int? _threads;
    private int _refreshTick;
    private bool _volumeReadInFlight;

    public PerformanceViewModel(IHardwareMonitor monitor, ISystemInfoSource system, DpcSampleViewModel dpc,
        IVolumeSource? volumes = null)
    {
        _monitor = monitor;
        _system = system;
        _volumes = volumes;
        Dpc = dpc;
    }

    public MetricSeriesViewModel Cpu { get; } = new(fixedMax: 100);
    public MetricSeriesViewModel Memory { get; } = new(fixedMax: null, autoFloor: 1);
    public MetricSeriesViewModel Disk { get; } = new(fixedMax: null, autoFloor: 10);
    public MetricSeriesViewModel Network { get; } = new(fixedMax: null, autoFloor: 1);
    public MetricSeriesViewModel Gpu { get; } = new(fixedMax: 100);

    public ObservableCollection<CoreLoadViewModel> Cores { get; } = new();
    public string CoresHeader => Cores.Count > 0 ? FormattableString.Invariant($"CPU CORES ({Cores.Count})") : "CPU CORES";
    public bool HasCores => Cores.Count > 0;

    public ObservableCollection<TempRowViewModel> Temps { get; } = new();
    public bool HasTemps => Temps.Count > 0;

    public ObservableCollection<DiskRowViewModel> Disks { get; } = new();
    public ObservableCollection<DiskVolumeRowViewModel> Volumes { get; } = new();
    public bool HasDisks => Disks.Count > 0 || Volumes.Count > 0;

    public ObservableCollection<KeyValueRow> SystemRows { get; } = new()
    {
        new KeyValueRow("CPU", "—"),
        new KeyValueRow("Cores / threads", "—"),
        new KeyValueRow("Memory", "—"),
        new KeyValueRow("GPU", "—"),
        new KeyValueRow("Motherboard", "—"),
        new KeyValueRow("OS", "—"),
        new KeyValueRow("Uptime", "—", isMono: true),
        new KeyValueRow("Processes", "—", isLast: true),
    };

    public DpcSampleViewModel Dpc { get; }

    public async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            (HardwareSnapshot Snapshot, int? Processes, TimeSpan? Uptime) read;
            try
            {
                read = await Task.Run(() => (_monitor.Read(), _system.ProcessCount(), (TimeSpan?)_system.Uptime()));
            }
            catch
            {
                read = (HardwareSnapshot.Empty, null, null);
            }

            if (!ReferenceEquals(read.Snapshot, HardwareSnapshot.Empty))
                Apply(read.Snapshot);
            Row("Uptime").Value = read.Uptime is TimeSpan up ? SystemInfoFormat.Uptime(up) : "—";
            Row("Processes").Value = SystemInfoFormat.Processes(read.Processes);

            _refreshTick++;
            if (_volumes is not null && (_refreshTick == 1 || _refreshTick % 5 == 0) && !_volumeReadInFlight)
            {
                _volumeReadInFlight = true;
                _ = RefreshVolumesAsync();
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    public async Task LoadSystemInfoAsync()
    {
        if (_systemLoaded || _systemLoading) return;
        _systemLoading = true;
        try
        {
            SystemInfo info;
            try { info = await Task.Run(() => _system.Read()); }
            catch { info = SystemInfo.Empty; }

            _cores = info.Cores;
            _threads = info.Threads;
            Row("CPU").Value = SystemInfoFormat.Cpu(info.CpuName, info.CpuBaseMhz);
            Row("Cores / threads").Value = SystemInfoFormat.CoresThreads(info.Cores, info.Threads);
            Row("Memory").Value = SystemInfoFormat.Memory(info.Memory);
            Row("GPU").Value = SystemInfoFormat.GpuNames(info.Gpus);
            Row("Motherboard").Value = SystemInfoFormat.Board(info.BoardMaker, info.BoardProduct);
            Row("OS").Value = SystemInfoFormat.Os(info.OsName, info.OsVersion, info.OsBuild);
            _systemLoaded = true;
        }
        finally
        {
            _systemLoading = false;
        }
    }

    private void Apply(HardwareSnapshot s)
    {
        Cpu.Push(s.CpuLoad);
        Cpu.SubText = CpuSubText(s.CpuClockMhz);

        Memory.SetMaximum(s.MemTotalGb);
        Memory.Push(s.MemUsedGb);
        Memory.SubText = s.MemTotalGb is double total ? "of " + MetricFormat.Value(total) + " GB" : "";

        Disk.Push(s.DiskBytesPerSec / MetricFormat.BytesPerMiB);
        Disk.SubText = "all drives";

        Network.Push(s.NetBytesPerSec / MetricFormat.BytesPerMiB);
        Network.SubText = s.NetAdapter ?? "";

        GpuReading? gpu = PrimaryGpu(s);
        Gpu.Push(gpu?.Load);
        Gpu.SubText = gpu?.Name ?? "";

        ApplyCores(s.CoreLoads);
        ApplyTemps(s, gpu);
        ApplyDisks(s.Disks);
    }

    private static GpuReading? PrimaryGpu(HardwareSnapshot s) => s.Gpus.OrderBy(g => g.Vendor).FirstOrDefault();

    private string CpuSubText(double? clockMhz)
    {
        var parts = new List<string>();
        if (clockMhz is not null) parts.Add(MetricFormat.ClockGhz(clockMhz));
        if (_cores is int c && _threads is int t) parts.Add(FormattableString.Invariant($"{c}C / {t}T"));
        return string.Join(" · ", parts);
    }

    private void ApplyCores(IReadOnlyList<double> loads)
    {
        if (Cores.Count != loads.Count)
        {
            Cores.Clear();
            for (int i = 0; i < loads.Count; i++) Cores.Add(new CoreLoadViewModel(i));
            OnPropertyChanged(nameof(CoresHeader));
            OnPropertyChanged(nameof(HasCores));
        }
        for (int i = 0; i < loads.Count; i++) Cores[i].Set(loads[i]);
    }

    private void ApplyTemps(HardwareSnapshot s, GpuReading? gpu)
    {
        var rows = new List<(string Label, double TempC)>();
        void Add(string label, double? tempC)
        {
            if (tempC is double t) rows.Add((label, t));
        }

        Add("CPU package", s.CpuPackageC);
        Add("CPU hottest core", s.CpuHottestCoreC);
        Add("GPU core", gpu?.TempC);
        Add("GPU hotspot", gpu?.HotSpotC);
        foreach (DriveTemp d in s.DriveTemps) Add(d.Name, d.TempC);

        bool sameRows = Temps.Count == rows.Count && Temps.Select(t => t.Label).SequenceEqual(rows.Select(r => r.Label));
        if (!sameRows)
        {
            Temps.Clear();
            for (int i = 0; i < rows.Count; i++) Temps.Add(new TempRowViewModel(rows[i].Label, isLast: i == rows.Count - 1));
            OnPropertyChanged(nameof(HasTemps));
        }
        for (int i = 0; i < rows.Count; i++) Temps[i].Set(rows[i].TempC);
    }

    private void ApplyDisks(IReadOnlyList<DiskReading> disks)
    {
        KeyedSync.Apply(Disks, disks, d => d.Name, r => r.Name, d => new DiskRowViewModel(d), (r, d) => r.Update(d));
        MarkLastDiskRows();
        OnPropertyChanged(nameof(HasDisks));
    }

    private void MarkLastDiskRows()
    {
        for (int i = 0; i < Disks.Count; i++) Disks[i].IsLast = i == Disks.Count - 1 && Volumes.Count == 0;
        for (int i = 0; i < Volumes.Count; i++) Volumes[i].IsLast = i == Volumes.Count - 1;
    }

    private async Task RefreshVolumesAsync()
    {
        try
        {
            IReadOnlyList<VolumeInfo> volumes;
            try
            {
                volumes = await Task.Run(() => _volumes!.Read());
            }
            catch
            {
                return;
            }

            KeyedSync.Apply(Volumes, volumes, v => v.Name, r => r.Name, v => new DiskVolumeRowViewModel(v), (r, v) => r.Update(v));
            MarkLastDiskRows();
            OnPropertyChanged(nameof(HasDisks));
        }
        finally
        {
            _volumeReadInFlight = false;
        }
    }

    private KeyValueRow Row(string key) => SystemRows.First(r => r.Key == key);

    public void Dispose() => _monitor.Dispose();
}
