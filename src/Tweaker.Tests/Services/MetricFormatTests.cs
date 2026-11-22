using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class MetricFormatTests
{
    [Fact] public void Nulls_render_as_dash()
    {
        Assert.Equal("—", MetricFormat.Load(null));
        Assert.Equal("—", MetricFormat.Temp(null));
        Assert.Equal("—", MetricFormat.ClockGhz(null));
        Assert.Equal("—", MetricFormat.Value(null));
    }

    [Fact] public void Values_format_invariant()
    {
        Assert.Equal("72%", MetricFormat.Load(72.4));
        Assert.Equal("64°C", MetricFormat.Temp(63.6));
        Assert.Equal("4.85 GHz", MetricFormat.ClockGhz(4850));
    }

    [Fact] public void Card_values_use_one_decimal_under_ten()
    {
        Assert.Equal("0.0", MetricFormat.Value(0));
        Assert.Equal("7.3", MetricFormat.Value(7.26));
        Assert.Equal("9.9", MetricFormat.Value(9.94));
        Assert.Equal("10", MetricFormat.Value(10));
        Assert.Equal("25", MetricFormat.Value(24.6));
        Assert.Equal("10", MetricFormat.Value(9.96));
    }
}
