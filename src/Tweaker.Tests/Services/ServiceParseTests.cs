using Tweaker.Core.Services;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ServiceParseTests
{
    private const string WinDir = @"C:\Windows";

    [Fact]
    public void Wmi_states_map_to_service_states()
    {
        Assert.Equal(ServiceState.Running, ServiceParse.State("Running"));
        Assert.Equal(ServiceState.Stopped, ServiceParse.State("Stopped"));
        Assert.Equal(ServiceState.StartPending, ServiceParse.State("Start Pending"));
        Assert.Equal(ServiceState.StopPending, ServiceParse.State("Stop Pending"));
        Assert.Equal(ServiceState.Paused, ServiceParse.State("Paused"));
        Assert.Equal(ServiceState.Unknown, ServiceParse.State(null));
    }

    [Fact]
    public void Wmi_start_modes_map_to_startup_types()
    {
        Assert.Equal(ServiceStartup.Automatic, ServiceParse.Startup("Auto", delayed: false));
        Assert.Equal(ServiceStartup.AutomaticDelayed, ServiceParse.Startup("Auto", delayed: true));
        Assert.Equal(ServiceStartup.Manual, ServiceParse.Startup("Manual", false));
        Assert.Equal(ServiceStartup.Disabled, ServiceParse.Startup("Disabled", false));
        Assert.Equal(ServiceStartup.Unknown, ServiceParse.Startup(null, false));
    }

    [Fact]
    public void The_image_path_drops_quotes_and_arguments_and_resolves_windows_prefixes()
    {
        Assert.Equal(@"C:\Program Files\NVIDIA Corporation\Display.NvContainer\NVDisplay.Container.exe",
            ServiceParse.ImagePath("\"C:\\Program Files\\NVIDIA Corporation\\Display.NvContainer\\NVDisplay.Container.exe\" -s NVDisplay.ContainerLocalSystem", WinDir));
        Assert.Equal(@"C:\WINDOWS\system32\svchost.exe", ServiceParse.ImagePath(@"C:\WINDOWS\system32\svchost.exe -k netsvcs -p", WinDir));
        Assert.Equal(@"C:\Windows\System32\drivers\x.exe", ServiceParse.ImagePath(@"\SystemRoot\System32\drivers\x.exe", WinDir));
        Assert.Equal(@"C:\Windows\system32\lsass.exe", ServiceParse.ImagePath(@"%SystemRoot%\system32\lsass.exe", WinDir));
        Assert.Equal(@"C:\Tools\svc.exe", ServiceParse.ImagePath(@"\??\C:\Tools\svc.exe", WinDir));
        Assert.Null(ServiceParse.ImagePath("  ", WinDir));
    }

    [Fact]
    public void Sc_errors_come_from_the_win32_error_code_with_the_output_line_as_fallback()
    {
        Assert.Null(ServiceParse.ScError(new ProcessResult(0, "SERVICE_NAME: Spooler", "")));
        Assert.Equal(new System.ComponentModel.Win32Exception(1058).Message,
            ServiceParse.ScError(new ProcessResult(1058, "[SC] StartService FAILED 1058:\r\n\r\ngarbled?text\r\n", "")));
        Assert.Equal(new System.ComponentModel.Win32Exception(5).Message, ServiceParse.ScError(new ProcessResult(5, "", "")));
        Assert.Equal("custom reason", ServiceParse.ScError(new ProcessResult(987654, "[SC] Thing FAILED 987654:\r\n\r\ncustom reason\r\n", "")));
        Assert.Equal("sc.exe exited with code 987654", ServiceParse.ScError(new ProcessResult(987654, "", "")));
    }
}
