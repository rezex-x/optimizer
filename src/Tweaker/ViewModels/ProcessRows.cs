using System.Globalization;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed record ProcessData(ProcessSample Sample, ProcessKind Kind, bool IsProtected, double? Cpu, double? IoBytesPerSec, double? Gpu)
{
    public string Key =>
        Sample.Pid.ToString(CultureInfo.InvariantCulture) + ":" + Sample.CreatedTicks.ToString(CultureInfo.InvariantCulture);
}

public sealed class ProcessRowViewModel : ObservableObject
{
    private double? _cpu, _disk, _gpu;
    private long _ramBytes;
    private string _ramText = "";
    private bool _isConfirming;

    public ProcessRowViewModel(ProcessData data, bool ramPercent, long totalMemory)
    {
        Key = data.Key;
        Pid = data.Sample.Pid;
        Created = data.Sample.CreatedTicks;
        Name = data.Sample.Name;
        Owner = data.Sample.Owner;
        IsProtected = data.IsProtected;
        Update(data, ramPercent, totalMemory);
    }

    public string Key { get; }
    public int Pid { get; }
    public long Created { get; }
    public string Name { get; }
    public string Owner { get; }
    public bool IsUserOwner => Owner == "User";
    public bool IsProtected { get; }

    public double Cpu => _cpu ?? -1;
    public string CpuText => ProcessFormat.Percent(_cpu);
    public UsageLevel CpuLevel => ProcessFormat.Level(_cpu, ProcessFormat.CpuWarm, ProcessFormat.CpuHot);

    public long RamBytes => _ramBytes;
    public string RamText { get => _ramText; private set => SetField(ref _ramText, value); }
    public UsageLevel RamLevel => ProcessFormat.Level(ProcessFormat.ToMegabytes(_ramBytes), ProcessFormat.RamWarmMb, ProcessFormat.RamHotMb);

    public double Disk => _disk ?? -1;
    public string DiskText => ProcessFormat.MegabytesPerSecond(_disk);
    public UsageLevel DiskLevel =>
        ProcessFormat.Level(_disk is double d ? ProcessFormat.ToMegabytes(d) : null, ProcessFormat.DiskWarmMb, ProcessFormat.DiskHotMb);

    public double Gpu => _gpu ?? -1;
    public string GpuText => ProcessFormat.Percent(_gpu);
    public UsageLevel GpuLevel => ProcessFormat.Level(_gpu, ProcessFormat.GpuWarm, ProcessFormat.GpuHot);

    public bool IsConfirming
    {
        get => _isConfirming;
        set { if (SetField(ref _isConfirming, value)) OnPropertyChanged(nameof(ShowEndTask)); }
    }

    public bool ShowEndTask => !IsProtected && !_isConfirming;

    public void Update(ProcessData data, bool ramPercent, long totalMemory)
    {
        _cpu = data.Cpu;
        _disk = data.IoBytesPerSec;
        _gpu = data.Gpu;
        _ramBytes = data.Sample.WorkingSetBytes;
        OnPropertyChanged(nameof(Cpu));
        OnPropertyChanged(nameof(CpuText));
        OnPropertyChanged(nameof(CpuLevel));
        OnPropertyChanged(nameof(RamBytes));
        OnPropertyChanged(nameof(RamLevel));
        OnPropertyChanged(nameof(Disk));
        OnPropertyChanged(nameof(DiskText));
        OnPropertyChanged(nameof(DiskLevel));
        OnPropertyChanged(nameof(Gpu));
        OnPropertyChanged(nameof(GpuText));
        OnPropertyChanged(nameof(GpuLevel));
        SetRamUnit(ramPercent, totalMemory);
    }

    public void SetRamUnit(bool percent, long totalMemory) =>
        RamText = percent ? ProcessFormat.MemoryPercent(_ramBytes, totalMemory) : ProcessFormat.Megabytes(_ramBytes);
}
