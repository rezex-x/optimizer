using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class DiagnosticsFormatTests
{
    [Fact]
    public void Nulls_and_unknowns_render_as_dash_or_unknown()
    {
        Assert.Equal("—", DiagnosticsFormat.Ms(null));
        Assert.Equal("—", DiagnosticsFormat.Jitter(null));
        Assert.Equal("—", DiagnosticsFormat.Loss(null));
        Assert.Equal("—", DiagnosticsFormat.Pct(null));
        Assert.Equal("—", DiagnosticsFormat.Rate(null));
        Assert.Equal("—", DiagnosticsFormat.Gb(null));
        Assert.Equal("—", DiagnosticsFormat.UsedOfTotalGb(null, 100.0));
        Assert.Equal("—", DiagnosticsFormat.UsedOfTotalGb(10.0, null));
        Assert.Equal("—", DiagnosticsFormat.Kind(DriveKind.Unknown));
        Assert.Equal("Unknown", DiagnosticsFormat.Smart(SmartHealth.Unknown));
    }

    [Fact]
    public void Values_format_invariant()
    {
        Assert.Equal("23.4 ms", DiagnosticsFormat.Ms(23.42));
        Assert.Equal("2.1 ms", DiagnosticsFormat.Jitter(2.14));
        Assert.Equal("0%", DiagnosticsFormat.Loss(0));
        Assert.Equal("1.5%", DiagnosticsFormat.Loss(1.5));
        Assert.Equal("1%", DiagnosticsFormat.Pct(1.42));
        Assert.Equal("13%", DiagnosticsFormat.Pct(12.6));
        Assert.Equal("<1%", DiagnosticsFormat.Pct(0.33));
        Assert.Equal("0%", DiagnosticsFormat.Pct(0));
        Assert.Equal("1234 /s", DiagnosticsFormat.Rate(1234));
        Assert.Equal("931.5 GB", DiagnosticsFormat.Gb(931.51));
        Assert.Equal("267.0 / 500.0 GB", DiagnosticsFormat.UsedOfTotalGb(233.0, 500.0));
        Assert.Equal("SSD", DiagnosticsFormat.Kind(DriveKind.Ssd));
        Assert.Equal("HDD", DiagnosticsFormat.Kind(DriveKind.Hdd));
        Assert.Equal("Healthy", DiagnosticsFormat.Smart(SmartHealth.Ok));
        Assert.Equal("Predicted failure", DiagnosticsFormat.Smart(SmartHealth.PredictedFail));
    }
}
