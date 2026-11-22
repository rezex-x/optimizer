using System;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ProcessRatesTests
{
    private static ProcessSample P(int pid, long created, double cpuSeconds, long io) =>
        new(pid, created, "p.exe", @"C:\p.exe", TimeSpan.FromSeconds(cpuSeconds), 0, io, "User", false, false);

    private static ProcessSnapshot At(double seconds, int cores, params ProcessSample[] processes) =>
        new(TimeSpan.FromSeconds(seconds), processes, 16L << 30, cores);

    [Fact]
    public void Cpu_percent_is_a_share_of_all_logical_processors()
    {
        var rates = ProcessRates.Compute(At(10, 8, P(100, 1, 5, 0)), At(12, 8, P(100, 1, 9, 0)));
        Assert.Equal(25.0, rates[(100, 1)].CpuPercent!.Value, 6);
    }

    [Fact]
    public void Io_rate_is_bytes_per_second()
    {
        var rates = ProcessRates.Compute(At(1, 4, P(100, 1, 0, 1_000)), At(3, 4, P(100, 1, 0, 2_001_000)));
        Assert.Equal(1_000_000, rates[(100, 1)].IoBytesPerSec!.Value, 6);
    }

    [Fact]
    public void A_reused_pid_gets_no_rate_and_counters_that_go_down_read_as_zero()
    {
        var rates = ProcessRates.Compute(
            At(1, 4, P(100, 1, 50, 9_000), P(200, 7, 3, 500)),
            At(2, 4, P(100, 2, 1, 10), P(200, 7, 1, 10)));

        Assert.False(rates.ContainsKey((100, 2)));
        Assert.Equal(new ProcessRate(0, 0), rates[(200, 7)]);
    }

    [Fact]
    public void Missing_counters_stay_unknown_and_no_elapsed_time_means_no_rates()
    {
        var before = At(1, 4, new ProcessSample(5, 1, "x.exe", null, null, 0, null, "SYSTEM", false, false));
        var after = At(2, 4, new ProcessSample(5, 1, "x.exe", null, null, 0, null, "SYSTEM", false, false));

        Assert.Equal(new ProcessRate(null, null), ProcessRates.Compute(before, after)[(5, 1)]);
        Assert.Empty(ProcessRates.Compute(after, after));
    }
}
