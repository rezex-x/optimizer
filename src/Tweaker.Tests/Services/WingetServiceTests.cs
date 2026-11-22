using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class WingetServiceTests
{
    [Fact]
    public void IsAvailable_true_when_version_exits_zero()
    {
        var runner = new FakeProcessRunner { NextResult = new ProcessResult(0, "v1.9", "") };
        Assert.True(new WingetService(runner).IsAvailable());
        Assert.Equal("winget", runner.Calls[0].fileName);
        Assert.Contains("--version", runner.Calls[0].arguments);
    }

    [Fact]
    public void IsAvailable_false_when_runner_throws()
    {
        var runner = new ThrowingProcessRunner();
        Assert.False(new WingetService(runner).IsAvailable());
    }

    [Fact]
    public void Install_builds_silent_exact_command_and_reports_success_on_zero()
    {
        var runner = new FakeProcessRunner { NextResult = new ProcessResult(0, "installed", "") };
        InstallResult r = new WingetService(runner).Install("Google.Chrome");

        string args = runner.Calls[0].arguments;
        Assert.Equal("winget", runner.Calls[0].fileName);
        Assert.Contains("install --id Google.Chrome -e", args);
        Assert.Contains("--silent", args);
        Assert.Contains("--accept-source-agreements", args);
        Assert.Contains("--accept-package-agreements", args);
        Assert.True(r.Success);
    }

    [Fact]
    public void Install_treats_already_installed_exit_code_as_success()
    {
        var runner = new FakeProcessRunner { NextResult = new ProcessResult(unchecked((int)0x8A15002B), "", "already installed") };
        Assert.True(new WingetService(runner).Install("VideoLAN.VLC").Success);
    }

    [Fact]
    public void Install_reports_failure_on_other_nonzero()
    {
        var runner = new FakeProcessRunner { NextResult = new ProcessResult(1, "", "boom") };
        InstallResult r = new WingetService(runner).Install("Bad.Id");
        Assert.False(r.Success);
        Assert.Equal(1, r.ExitCode);
    }
}
