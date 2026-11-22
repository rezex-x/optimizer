using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ServiceFormatTests
{
    [Fact]
    public void Status_startup_and_pid_read_like_services_msc()
    {
        Assert.Equal("Running", ServiceFormat.Status(ServiceState.Running));
        Assert.Equal("Starting…", ServiceFormat.Status(ServiceState.StartPending));
        Assert.Equal("Stopping…", ServiceFormat.Status(ServiceState.StopPending));
        Assert.Equal("Automatic (Delayed)", ServiceFormat.Startup(ServiceStartup.AutomaticDelayed));
        Assert.Equal("Disabled", ServiceFormat.Startup(ServiceStartup.Disabled));
        Assert.Equal("—", ServiceFormat.Startup(ServiceStartup.Unknown));
        Assert.Equal("1480", ServiceFormat.Pid(ServiceState.Running, 1480));
        Assert.Equal("—", ServiceFormat.Pid(ServiceState.Stopped, 0));
    }
}
