using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ProcessFormatTests
{
    [Fact]
    public void Values_format_invariant_with_dashes_for_unknown()
    {
        Assert.Equal("12.3", ProcessFormat.Percent(12.34));
        Assert.Equal("—", ProcessFormat.Percent(null));
        Assert.Equal("1248", ProcessFormat.Megabytes(1248L * 1024 * 1024));
        Assert.Equal("3.8", ProcessFormat.MemoryPercent(1248L * 1024 * 1024, 32L << 30));
        Assert.Equal("—", ProcessFormat.MemoryPercent(1, 0));
        Assert.Equal("0.8", ProcessFormat.MegabytesPerSecond(0.8 * 1024 * 1024));
        Assert.Equal("—", ProcessFormat.MegabytesPerSecond(null));
    }

    [Fact]
    public void Levels_grade_warm_and_hot()
    {
        Assert.Equal(UsageLevel.Normal, ProcessFormat.Level(9.9, ProcessFormat.CpuWarm, ProcessFormat.CpuHot));
        Assert.Equal(UsageLevel.Warm, ProcessFormat.Level(10, ProcessFormat.CpuWarm, ProcessFormat.CpuHot));
        Assert.Equal(UsageLevel.Hot, ProcessFormat.Level(30, ProcessFormat.CpuWarm, ProcessFormat.CpuHot));
        Assert.Equal(UsageLevel.Normal, ProcessFormat.Level(null, 1, 2));
    }
}
