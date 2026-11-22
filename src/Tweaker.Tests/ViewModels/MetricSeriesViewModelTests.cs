using System.Collections.Generic;
using System.Linq;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class MetricSeriesViewModelTests
{
    [Fact]
    public void A_missing_value_shows_a_dash_and_adds_no_history()
    {
        var s = new MetricSeriesViewModel(fixedMax: 100);
        s.Push(null);
        Assert.Equal("—", s.ValueText);
        Assert.Empty(s.History);
        Assert.Equal(0, s.Fraction);
    }

    [Fact]
    public void A_fixed_maximum_gives_the_usage_fraction()
    {
        var s = new MetricSeriesViewModel(fixedMax: 100);
        s.Push(25);
        Assert.Equal("25", s.ValueText);
        Assert.Equal(100, s.Maximum);
        Assert.Equal(0.25, s.Fraction, 6);
        Assert.Equal(new[] { 25.0 }, s.History);
    }

    [Fact]
    public void History_keeps_the_newest_40_samples()
    {
        var s = new MetricSeriesViewModel(fixedMax: 100);
        for (int i = 1; i <= 45; i++) s.Push(i);
        Assert.Equal(MetricSeriesViewModel.Capacity, s.History.Length);
        Assert.Equal(6, s.History[0]);
        Assert.Equal(45, s.History[^1]);
    }

    [Fact]
    public void Without_a_fixed_maximum_the_scale_follows_the_recent_peak_above_a_floor()
    {
        var s = new MetricSeriesViewModel(fixedMax: null, autoFloor: 10);
        s.Push(2);
        Assert.Equal(10, s.Maximum);
        Assert.Equal(0.2, s.Fraction, 6);
        s.Push(40);
        Assert.Equal(40, s.Maximum);
        Assert.Equal(1, s.Fraction, 6);
        s.Push(20);
        Assert.Equal(40, s.Maximum);
        Assert.Equal(0.5, s.Fraction, 6);
    }

    [Fact]
    public void SetMaximum_overrides_the_scale_until_cleared()
    {
        var s = new MetricSeriesViewModel(fixedMax: null, autoFloor: 1);
        s.SetMaximum(32);
        s.Push(8);
        Assert.Equal(32, s.Maximum);
        Assert.Equal(0.25, s.Fraction, 6);
        s.SetMaximum(null);
        Assert.Equal(8, s.Maximum);
    }

    [Fact]
    public void Negative_values_clamp_to_zero_and_the_fraction_never_exceeds_one()
    {
        var s = new MetricSeriesViewModel(fixedMax: 100);
        s.Push(-5);
        Assert.Equal("0.0", s.ValueText);
        Assert.Equal(new[] { 0.0 }, s.History);
        s.Push(150);
        Assert.Equal(1, s.Fraction);
    }

    [Fact]
    public void Push_notifies_the_bound_properties()
    {
        var s = new MetricSeriesViewModel(fixedMax: 100);
        var raised = new List<string>();
        s.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);
        s.Push(10);
        Assert.Contains(nameof(s.History), raised);
        Assert.Contains(nameof(s.ValueText), raised);
        Assert.Contains(nameof(s.Maximum), raised);
        Assert.Contains(nameof(s.Fraction), raised);
    }
}
