using System;
using System.Collections.Generic;
using System.Linq;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class MetricSeriesViewModel : ObservableObject
{
    public const int Capacity = 40;

    private readonly double? _fixedMax;
    private readonly double _autoFloor;
    private readonly Queue<double> _samples = new();
    private double? _latest;
    private double? _maxOverride;
    private string _subText = "";

    public MetricSeriesViewModel(double? fixedMax, double autoFloor = 1)
    {
        _fixedMax = fixedMax;
        _autoFloor = autoFloor;
    }

    public string ValueText => MetricFormat.Value(_latest);

    public double[] History { get; private set; } = Array.Empty<double>();

    public double Maximum => _maxOverride ?? _fixedMax ?? Math.Max(_autoFloor, History.Length == 0 ? 0 : History.Max());

    public double Fraction => _latest is double v && Maximum > 0 ? Math.Clamp(v / Maximum, 0, 1) : 0;

    public string SubText { get => _subText; set => SetField(ref _subText, value); }

    public void SetMaximum(double? maximum)
    {
        _maxOverride = maximum is > 0 ? maximum : null;
        OnPropertyChanged(nameof(Maximum));
        OnPropertyChanged(nameof(Fraction));
    }

    public void Push(double? value)
    {
        _latest = value is double d && double.IsFinite(d) ? Math.Max(0, d) : null;
        if (_latest is double v)
        {
            _samples.Enqueue(v);
            while (_samples.Count > Capacity) _samples.Dequeue();
            History = _samples.ToArray();
            OnPropertyChanged(nameof(History));
        }
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(Maximum));
        OnPropertyChanged(nameof(Fraction));
    }
}
