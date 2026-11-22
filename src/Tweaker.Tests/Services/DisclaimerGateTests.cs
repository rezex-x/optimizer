using System.IO;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class DisclaimerGateTests
{
    [Fact]
    public void Shows_when_flag_absent_then_hidden_after_acknowledge()
    {
        using var temp = new TempDir();
        string flag = Path.Combine(temp.Path, "disclaimer-ack");
        var gate = new DisclaimerGate(flag);

        Assert.True(gate.ShouldShow());
        gate.Acknowledge();
        Assert.False(gate.ShouldShow());
        Assert.False(new DisclaimerGate(flag).ShouldShow());
    }
}
