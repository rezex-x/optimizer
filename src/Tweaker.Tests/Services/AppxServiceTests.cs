using System;
using System.Linq;
using System.Text;
using Tweaker.Core.Services;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class AppxServiceTests
{
    [Fact]
    public void Installed_names_are_parsed_one_per_line()
    {
        var set = AppxService.ParseNames("Microsoft.BingNews\r\n\r\nClipchamp.Clipchamp\n  Microsoft.Copilot  \n");
        Assert.Equal(new[] { "Clipchamp.Clipchamp", "Microsoft.BingNews", "Microsoft.Copilot" }, set.OrderBy(s => s).ToArray());
        Assert.Contains("clipchamp.clipchamp", set);
    }

    [Fact]
    public void A_failed_listing_returns_null()
    {
        var runner = new FakeProcessRunner { NextResult = new ProcessResult(1, "", "boom") };
        Assert.Null(new AppxService(runner).InstalledPackageNames());
    }

    [Fact]
    public void A_listing_with_errors_but_names_keeps_the_names()
    {
        var runner = new FakeProcessRunner { NextResult = new ProcessResult(1, "Microsoft.BingNews\r\nClipchamp.Clipchamp\r\n", "orphaned SID") };

        var names = new AppxService(runner).InstalledPackageNames();

        Assert.NotNull(names);
        Assert.Equal(2, names!.Count);
        (_, string args) = Assert.Single(runner.Calls);
        Assert.Contains("-NonInteractive", args);
        Assert.Contains("$ProgressPreference = 'SilentlyContinue'", args);
    }

    [Fact]
    public void The_removal_script_removes_every_copy_and_the_provisioned_package()
    {
        string script = AppxService.BuildRemovalScript(new[] { "Clipchamp.Clipchamp", "Microsoft.BingNews" });

        Assert.Contains("'Clipchamp.Clipchamp'", script);
        Assert.Contains("'Microsoft.BingNews'", script);
        Assert.Contains("$ProgressPreference = 'SilentlyContinue'", script);
        Assert.Contains("Remove-AppxProvisionedPackage -Online", script);
        Assert.Contains("Remove-AppxPackage -Package $p.PackageFullName -AllUsers", script);
        Assert.Contains("Get-AppxPackage -AllUsers -Name $n", script);
        Assert.DoesNotContain("OutlookUpdate", script);
        Assert.DoesNotContain("DevHomeUpdate", script);
    }

    [Fact]
    public void Removing_Outlook_or_Dev_Home_also_removes_their_reinstall_keys()
    {
        string outlook = AppxService.BuildRemovalScript(new[] { "Microsoft.OutlookForWindows" });
        Assert.Contains(@"Remove-Item -Path 'HKLM:\SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\OutlookUpdate' -Recurse -Force -ErrorAction SilentlyContinue", outlook);
        Assert.DoesNotContain("DevHomeUpdate", outlook);

        string devHome = AppxService.BuildRemovalScript(new[] { "Clipchamp.Clipchamp", "Microsoft.Windows.DevHome" });
        Assert.Contains(@"UScheduler_Oobe\DevHomeUpdate' -Recurse -Force", devHome);
        Assert.DoesNotContain("OutlookUpdate", devHome);
    }

    [Fact]
    public void Each_app_gets_its_status_from_its_output_line()
    {
        var names = new[] { "A.Removed", "B.Missing", "C.Failed", "D.Stuck" };
        var output = new ProcessResult(0, "A.Removed|removed|\r\nB.Missing|missing|\r\nC.Failed|failed|Deployment failed 0x80073CFA\r\nD.Stuck|failed|\r\n", "");

        var results = AppxService.ParseRemoval(names, output);

        Assert.Equal(names, results.Select(r => r.PackageName));
        Assert.Equal(new AppxRemovalResult("A.Removed", AppxRemoval.Removed, ""), results[0]);
        Assert.Equal(new AppxRemovalResult("B.Missing", AppxRemoval.NotInstalled, ""), results[1]);
        Assert.Equal(new AppxRemovalResult("C.Failed", AppxRemoval.Failed, "Deployment failed 0x80073CFA"), results[2]);
        Assert.Equal(new AppxRemovalResult("D.Stuck", AppxRemoval.Failed, "still installed"), results[3]);
    }

    [Fact]
    public void An_app_without_a_line_fails_with_a_clean_stderr_reason()
    {
        const string stderr = "#< CLIXML\r\n<Objs Version=\"1.1.0.1\" xmlns=\"http://schemas.microsoft.com/powershell/2004/04\"><S S=\"Error\">x</S></Objs>\r\nAccess is denied.\r\n";
        var results = AppxService.ParseRemoval(new[] { "A.Done", "B.Lost" }, new ProcessResult(1, "A.Done|removed|\r\n", stderr));

        Assert.Equal(AppxRemoval.Removed, results[0].Result);
        Assert.Equal(new AppxRemovalResult("B.Lost", AppxRemoval.Failed, "Access is denied."), results[1]);

        var clixmlOnly = AppxService.ParseRemoval(new[] { "B.Lost" }, new ProcessResult(3, "", "#< CLIXML\r\n<Objs Version=\"1.1.0.1\"></Objs>"));
        Assert.Equal("PowerShell exited with code 3", clixmlOnly[0].Reason);
    }

    [Fact]
    public void A_timed_out_run_fails_the_unfinished_apps()
    {
        var results = AppxService.ParseRemoval(new[] { "A.Done", "B.Slow" }, new ProcessResult(-1, "A.Done|removed|\r\n", "timed out after 600 s"));

        Assert.Equal(AppxRemoval.Removed, results[0].Result);
        Assert.Equal(new AppxRemovalResult("B.Slow", AppxRemoval.Failed, "timed out after 600 s"), results[1]);
    }

    [Fact]
    public void RemoveMany_refuses_unsafe_names_and_runs_one_encoded_script()
    {
        const string unsafeName = "x'; Remove-Item C:\\ -Recurse; '";
        var runner = new FakeProcessRunner { NextResult = new ProcessResult(0, "Clipchamp.Clipchamp|removed|\r\n", "") };

        var results = new AppxService(runner).RemoveMany(new[] { unsafeName, "Clipchamp.Clipchamp" });

        Assert.Equal(new AppxRemovalResult(unsafeName, AppxRemoval.Failed, "unsupported package name"), results[0]);
        Assert.Equal(new AppxRemovalResult("Clipchamp.Clipchamp", AppxRemoval.Removed, ""), results[1]);
        (string file, string args) = Assert.Single(runner.Calls);
        Assert.Equal("powershell.exe", file);
        Assert.StartsWith("-NoProfile -NonInteractive -EncodedCommand ", args);
        string script = Encoding.Unicode.GetString(Convert.FromBase64String(args.Split(' ').Last()));
        Assert.Contains("'Clipchamp.Clipchamp'", script);
        Assert.DoesNotContain("Remove-Item C:", script);
    }

    [Fact]
    public void RemoveMany_with_only_unsafe_names_runs_nothing()
    {
        var runner = new FakeProcessRunner();

        var results = new AppxService(runner).RemoveMany(new[] { "bad name" });

        Assert.Equal(AppxRemoval.Failed, Assert.Single(results).Result);
        Assert.Empty(runner.Calls);
    }
}
