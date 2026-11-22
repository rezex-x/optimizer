using Xunit;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;

namespace Tweaker.Tests.Services;

public class RestorePointServiceTests
{
    [Fact]
    public void Create_invokes_powershell_checkpoint_computer()
    {
        var runner = new FakeProcessRunner { NextResult = new ProcessResult(0, "", "") };
        var svc = new RestorePointService(runner);

        RestorePointResult result = svc.Create("Tweaker session");

        Assert.Equal("powershell.exe", runner.Calls[0].fileName);
        Assert.Contains("Checkpoint-Computer", runner.Calls[0].arguments);
        Assert.Contains("Tweaker session", runner.Calls[0].arguments);
        Assert.True(result.Success);
    }

    [Fact]
    public void Create_detects_protection_disabled()
    {
        var runner = new FakeProcessRunner
        {
            NextResult = new ProcessResult(1, "", "system protection is turned off")
        };
        var svc = new RestorePointService(runner);

        RestorePointResult result = svc.Create("x");

        Assert.False(result.Success);
        Assert.True(result.ProtectionDisabled);
    }

    [Fact]
    public void Create_escapes_single_quotes_in_description()
    {
        var runner = new FakeProcessRunner { NextResult = new ProcessResult(0, "", "") };
        var svc = new RestorePointService(runner);

        RestorePointResult result = svc.Create("User's session");

        Assert.Contains("User''s", runner.Calls[0].arguments);
        Assert.True(result.Success);
    }
}
