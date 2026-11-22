using System;
using System.Collections.Generic;
using System.Linq;

namespace Tweaker.ViewModels;

public sealed class FreedResourcesViewModel : ObservableObject
{
    private const double MbPerGb = 1024;

    private static readonly string[] Properties =
    {
        nameof(MeasuredMb), nameof(EstimatedMb), nameof(TotalMb), nameof(TotalText), nameof(HasTotal), nameof(TooltipText),
    };

    private const string ServicePrefix = "service|";

    private static readonly IReadOnlySet<string> NoServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly Func<int>? _estimatedMb;
    private readonly Func<IReadOnlySet<string>>? _coveredServices;
    private readonly Dictionary<string, double> _measured = new(StringComparer.Ordinal);
    private int _estimated;
    private IReadOnlySet<string> _covered = NoServices;

    public FreedResourcesViewModel(Func<int>? estimatedMb = null, Func<IReadOnlySet<string>>? coveredServices = null)
    {
        _estimatedMb = estimatedMb;
        _coveredServices = coveredServices;
        _estimated = ReadEstimate();
        _covered = ReadCovered();
    }

    public double MeasuredMb => _measured.Where(e => !IsCovered(e.Key)).Sum(e => e.Value);
    public int EstimatedMb => _estimated;
    public double TotalMb => _estimated + MeasuredMb;

    public string TotalText
    {
        get
        {
            double total = TotalMb;
            if (total < 1) return "";
            return total < MbPerGb
                ? FormattableString.Invariant($"Freed: ~{total:0} MB RAM")
                : FormattableString.Invariant($"Freed: ~{total / MbPerGb:0.0} GB RAM");
        }
    }

    public bool HasTotal => TotalText.Length > 0;

    public string TooltipText => FormattableString.Invariant(
        $"Applied tweaks (estimated): ~{_estimated} MB\nStopped or ended this session (measured): ~{MeasuredMb:0} MB");

    public void RecordStopped(string key, double mb)
    {
        if (string.IsNullOrEmpty(key) || double.IsNaN(mb) || double.IsInfinity(mb)) return;
        _measured[key] = Math.Max(0, mb);
        RaiseAll();
    }

    public void Forget(string key)
    {
        if (!string.IsNullOrEmpty(key) && _measured.Remove(key)) RaiseAll();
    }

    public void Refresh()
    {
        int estimate = ReadEstimate();
        IReadOnlySet<string> covered = ReadCovered();
        if (estimate == _estimated && covered.SetEquals(_covered)) return;
        _estimated = estimate;
        _covered = covered;
        RaiseAll();
    }

    private bool IsCovered(string key) =>
        _covered.Count > 0
        && key.StartsWith(ServicePrefix, StringComparison.Ordinal)
        && _covered.Any(name => string.Equals(name, key.Substring(ServicePrefix.Length), StringComparison.OrdinalIgnoreCase));

    private IReadOnlySet<string> ReadCovered()
    {
        if (_coveredServices is null) return NoServices;
        try { return _coveredServices() ?? NoServices; }
        catch { return _covered; }
    }

    private int ReadEstimate()
    {
        if (_estimatedMb is null) return 0;
        try { return Math.Max(0, _estimatedMb()); }
        catch { return _estimated; }
    }

    private void RaiseAll()
    {
        foreach (string name in Properties) OnPropertyChanged(name);
    }
}
