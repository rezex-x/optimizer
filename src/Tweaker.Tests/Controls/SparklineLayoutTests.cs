using System;
using System.Collections.Generic;
using System.Linq;
using Tweaker.Controls;
using Xunit;

namespace Tweaker.Tests.Controls;

public class SparklineLayoutTests
{
    private static IReadOnlyList<SparkBar> Layout(double[] values, double max) =>
        SparklineLayout.Compute(values, max, slots: 4, width: 43, height: 20);

    [Fact]
    public void Newest_values_fill_the_right_most_slots()
    {
        var bars = Layout(new[] { 0.0, 5.0, 10.0 }, max: 10);
        Assert.Equal(3, bars.Count);
        Assert.Equal(new[] { 11.0, 22.0, 33.0 }, bars.Select(b => b.X));
        Assert.All(bars, b => Assert.Equal(10, b.Width, 6));
        Assert.Equal(20 * SparklineLayout.MinFraction, bars[0].Height, 6);
        Assert.Equal(10, bars[1].Height, 6);
        Assert.Equal(20, bars[2].Height, 6);
        Assert.Equal(20 - bars[1].Height, bars[1].Y, 6);
    }

    [Fact]
    public void Only_the_newest_values_that_fit_are_drawn()
    {
        var bars = Layout(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, max: 6);
        Assert.Equal(4, bars.Count);
        Assert.Equal(0, bars[0].X, 6);
        Assert.Equal(20 * 3 / 6.0, bars[0].Height, 6);
    }

    [Fact]
    public void Values_above_the_maximum_are_clamped()
        => Assert.Equal(20, Layout(new[] { 50.0 }, max: 10)[0].Height, 6);

    [Fact]
    public void NaN_maximum_scales_to_the_largest_value()
    {
        var bars = Layout(new[] { 2.0, 4.0 }, max: double.NaN);
        Assert.Equal(10, bars[0].Height, 6);
        Assert.Equal(20, bars[1].Height, 6);
    }

    [Fact]
    public void Older_bars_are_fainter()
    {
        var bars = Layout(new[] { 1.0, 1.0, 1.0, 1.0 }, max: 1);
        Assert.True(bars[0].Opacity < bars[1].Opacity && bars[1].Opacity < bars[3].Opacity);
        Assert.Equal(0.4, bars[0].Opacity, 6);
    }

    [Fact]
    public void Nothing_to_draw_without_values_or_space()
    {
        Assert.Empty(Layout(Array.Empty<double>(), max: 10));
        Assert.Empty(SparklineLayout.Compute(new[] { 1.0 }, 10, slots: 4, width: 0, height: 20));
    }

    [Fact]
    public void Bars_snap_to_whole_pixels_and_stay_right_aligned()
    {
        var bars = SparklineLayout.Compute(Enumerable.Repeat(1.0, 40).ToArray(), 1, slots: 40, width: 137, height: 34);
        Assert.All(bars, b => Assert.Equal(Math.Round(b.X), b.X, 6));
        Assert.All(bars, b => Assert.Equal(2, b.Width, 6));
        Assert.Equal(137, bars[^1].X + bars[^1].Width, 6);
    }
}
