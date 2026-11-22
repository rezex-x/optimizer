using Tweaker.Core.Services;
using Xunit;

namespace Tweaker.Tests.TestSupport;

public class FakeNvidiaSettingsServiceTests
{
    [Fact]
    public void Apply_records_call_and_returns_prior()
    {
        var svc = new FakeNvidiaSettingsService();
        svc.Values[0x1057EB71] = 5;

        uint? prior = svc.ApplySetting(0x1057EB71, 1);

        Assert.Equal(5u, prior);
        Assert.Equal(1u, svc.ReadSetting(0x1057EB71));
        Assert.Contains((0x1057EB71u, 1u), svc.Applied);
    }

    [Fact]
    public void Apply_returns_null_prior_when_absent()
    {
        var svc = new FakeNvidiaSettingsService();
        Assert.Null(svc.ApplySetting(0x00AC8497, 10240));
    }
}
