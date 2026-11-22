using System;
using System.Globalization;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class CoreLoadViewModel : ObservableObject
{
    private double _load;

    public CoreLoadViewModel(int index) => Label = FormattableString.Invariant($"Core {index}");

    public string Label { get; }

    public double Load
    {
        get => _load;
        private set
        {
            if (!SetField(ref _load, value)) return;
            OnPropertyChanged(nameof(LoadText));
            OnPropertyChanged(nameof(IsHigh));
        }
    }

    public string LoadText => MetricFormat.Load(_load);

    public bool IsHigh => _load > 80;

    public void Set(double load) => Load = Math.Clamp(load, 0, 100);
}

public sealed class TempRowViewModel : ObservableObject
{
    private double _tempC;

    public TempRowViewModel(string label, bool isLast = false)
    {
        Label = label;
        IsLast = isLast;
    }

    public string Label { get; }
    public bool IsLast { get; }

    public double TempC
    {
        get => _tempC;
        private set
        {
            if (!SetField(ref _tempC, value)) return;
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(BarValue));
            OnPropertyChanged(nameof(IsWarm));
            OnPropertyChanged(nameof(IsHot));
        }
    }

    public string Text => MetricFormat.Temp(_tempC);

    public double BarValue => Math.Clamp(_tempC, 0, 100);

    public bool IsWarm => _tempC >= 65 && _tempC < 80;
    public bool IsHot => _tempC >= 80;

    public void Set(double tempC) => TempC = tempC;
}

public sealed class DiskRowViewModel : ObservableObject
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const double BytesPerMB = 1024.0 * 1024.0;

    private double? _activityPct;
    private double? _readBps;
    private double? _writeBps;
    private double? _usedPct;
    private double? _tempC;
    private bool _isLast;

    public DiskRowViewModel(DiskReading reading)
    {
        Name = reading.Name;
        Update(reading);
    }

    public string Name { get; }

    public bool IsLast { get => _isLast; set => SetField(ref _isLast, value); }

    public string ActivityText => _activityPct is double a ? a.ToString("0", Inv) + " %" : "—";

    public double ActivityFraction => _activityPct is double a ? Math.Clamp(a / 100.0, 0, 1) : 0;

    public string RatesText => "R " + Rate(_readBps) + " · W " + Rate(_writeBps);

    public string UsedText => _usedPct is double u ? "Used " + u.ToString("0", Inv) + " %" : "";

    public string TempText => _tempC is double t ? t.ToString("0", Inv) + " °C" : "";

    public void Update(DiskReading reading)
    {
        _activityPct = reading.ActivityPct;
        _readBps = reading.ReadBps;
        _writeBps = reading.WriteBps;
        _usedPct = reading.UsedPct;
        _tempC = reading.TempC;
        OnPropertyChanged(nameof(ActivityText));
        OnPropertyChanged(nameof(ActivityFraction));
        OnPropertyChanged(nameof(RatesText));
        OnPropertyChanged(nameof(UsedText));
        OnPropertyChanged(nameof(TempText));
    }

    private static string Rate(double? bps) => bps is double b ? (b / BytesPerMB).ToString("0.0", Inv) + " MB/s" : "—";
}

public sealed class DiskVolumeRowViewModel : ObservableObject
{
    private const double BytesPerGB = 1024.0 * 1024.0 * 1024.0;
    private string _text = "";
    private bool _isLast;

    public DiskVolumeRowViewModel(VolumeInfo info)
    {
        Name = info.Name;
        Update(info);
    }

    public string Name { get; }

    public bool IsLast { get => _isLast; set => SetField(ref _isLast, value); }
    public string Text { get => _text; private set => SetField(ref _text, value); }

    public void Update(VolumeInfo info)
    {
        long totalGb = (long)Math.Round(info.TotalBytes / BytesPerGB, MidpointRounding.AwayFromZero);
        long usedGb = (long)Math.Round((info.TotalBytes - info.FreeBytes) / BytesPerGB, MidpointRounding.AwayFromZero);
        string label = string.IsNullOrEmpty(info.Label) ? "" : info.Label + " · ";
        Text = FormattableString.Invariant($"{info.Name}  {label}{usedGb} of {totalGb} GB used");
    }
}

public sealed class KeyValueRow : ObservableObject
{
    private string _value;

    public KeyValueRow(string key, string value, bool isMono = false, bool isLast = false)
    {
        Key = key;
        _value = value;
        IsMono = isMono;
        IsLast = isLast;
    }

    public string Key { get; }
    public string Value { get => _value; set => SetField(ref _value, value); }
    public bool IsMono { get; }
    public bool IsLast { get; }
}
