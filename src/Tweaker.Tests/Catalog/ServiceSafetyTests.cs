using Tweaker.Core.Catalog;
using Xunit;

namespace Tweaker.Tests.Catalog;

public class ServiceSafetyTests
{
    [Theory]
    [InlineData("Spooler", SafetyLevel.Safe)]
    [InlineData("DiagTrack", SafetyLevel.Safe)]
    [InlineData("spooler", SafetyLevel.Safe)]
    [InlineData("SysMain", SafetyLevel.Caution)]
    [InlineData("XboxGipSvc", SafetyLevel.Caution)]
    [InlineData("wuauserv", SafetyLevel.Caution)]
    [InlineData("WinDefend", SafetyLevel.Keep)]
    [InlineData("Dnscache", SafetyLevel.Keep)]
    [InlineData("vgc", SafetyLevel.Keep)]
    [InlineData("BEService", SafetyLevel.Keep)]
    public void Known_services_have_advice(string name, SafetyLevel level)
    {
        ServiceAdvice? advice = ServiceSafety.For(name);
        Assert.NotNull(advice);
        Assert.Equal(level, advice!.Level);
        Assert.False(string.IsNullOrWhiteSpace(advice.Reason));
    }

    [Fact]
    public void Per_user_services_match_their_base_name_and_unknown_services_get_nothing()
    {
        Assert.Equal(SafetyLevel.Caution, ServiceSafety.For("CDPSvc_4a3b2c1")!.Level);
        Assert.Null(ServiceSafety.For("SomeVendorService"));
        Assert.Null(ServiceSafety.For(""));
    }
}
