using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ServiceRulesTests
{
    private const string WinDir = @"C:\Windows";

    private static ServiceSample S(string name) =>
        new(name, name, "", ServiceState.Running, ServiceStartup.Automatic, 100, true, true);

    [Fact]
    public void Core_windows_security_and_anti_cheat_services_are_protected()
    {
        Assert.True(ServiceRules.IsProtected(S("RpcSs")));
        Assert.True(ServiceRules.IsProtected(S("winmgmt")));
        Assert.True(ServiceRules.IsProtected(S("WinDefend")));
        Assert.True(ServiceRules.IsProtected(S("EasyAntiCheat_EOS")));
        Assert.True(ServiceRules.IsProtected(S("vgc")));
        Assert.True(ServiceRules.IsProtected(S("Audiosrv")));
    }

    [Fact]
    public void Ordinary_services_can_be_stopped()
    {
        Assert.False(ServiceRules.IsProtected(S("Spooler")));
        Assert.False(ServiceRules.IsProtected(S("SysMain")));
        Assert.False(ServiceRules.IsProtected(S("wuauserv")));
    }

    [Fact]
    public void Windows_services_are_under_the_windows_folder_or_made_by_microsoft()
    {
        Assert.True(ServiceRules.IsWindows(@"C:\WINDOWS\system32\svchost.exe", null, WinDir));
        Assert.True(ServiceRules.IsWindows(@"C:\ProgramData\Microsoft\Windows Defender\Platform\4.18\MsMpEng.exe", "Microsoft Corporation", WinDir));
        Assert.False(ServiceRules.IsWindows(@"C:\Program Files\NVIDIA Corporation\Display.NvContainer\NVDisplay.Container.exe", "NVIDIA Corporation", WinDir));
        Assert.False(ServiceRules.IsWindows(null, null, WinDir));
    }

    [Fact]
    public void Driver_store_vendor_services_are_third_party()
    {
        const string nv = @"C:\Windows\System32\DriverStore\FileRepository\nv_dispi.inf_amd64_1\Display.NvContainer\NVDisplay.Container.exe";
        Assert.False(ServiceRules.IsWindows(nv, "NVIDIA Corporation", WinDir));
        Assert.False(ServiceRules.IsWindows(nv, null, WinDir));
        Assert.False(ServiceRules.IsWindows(@"C:\Windows\System32\amdfendrsr.exe", "Advanced Micro Devices, Inc.", WinDir));
        Assert.True(ServiceRules.IsWindows(@"C:\Windows\system32\svchost.exe", "Microsoft Corporation", WinDir));
        Assert.True(ServiceRules.IsWindows(@"C:\Windows\system32\svchost.exe", null, WinDir));
    }
}
