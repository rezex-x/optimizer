using Tweaker.Core.Models;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.TestSupport;

public class FakeConfirmationServiceTests
{
    [Fact]
    public void Records_requests_and_returns_configured_result()
    {
        var fake = new FakeConfirmationService { Result = false };
        var req = new TweakConfirmRequest("N", "C", RiskLevel.Low, false, "d", "b", "ds", null);

        Assert.False(fake.Confirm(req));
        Assert.Single(fake.SingleRequests);
        Assert.Same(req, fake.SingleRequests[0]);
    }
}
