using Xunit;
using Tweaker.Core.Models;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;

namespace Tweaker.Tests.Services;

public class BatchServiceTests
{
    [Fact]
    public void ApplyScript_runs_cmd_with_script()
    {
        var runner = new FakeProcessRunner { NextResult = new ProcessResult(0, "ok", "") };
        var svc = new BatchService(runner);
        var tweak = new Tweak { Type = TweakType.Batch, ApplyScript = "sc stop SysMain" };

        ProcessResult result = svc.ApplyScript(tweak);

        Assert.Equal("cmd.exe", runner.Calls[0].fileName);
        Assert.Contains("sc stop SysMain", runner.Calls[0].arguments);
        Assert.StartsWith("/c", runner.Calls[0].arguments);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void RevertScript_runs_revert_body()
    {
        var runner = new FakeProcessRunner();
        var svc = new BatchService(runner);
        var tweak = new Tweak { Type = TweakType.Batch, RevertScript = "sc start SysMain" };

        svc.RevertScript(tweak);

        Assert.Contains("sc start SysMain", runner.Calls[0].arguments);
    }

    [Fact]
    public void ApplyScript_throws_when_script_missing()
    {
        var svc = new BatchService(new FakeProcessRunner());
        var tweak = new Tweak { Type = TweakType.Batch, ApplyScript = null };

        Assert.Throws<InvalidOperationException>(() => svc.ApplyScript(tweak));
    }
}
