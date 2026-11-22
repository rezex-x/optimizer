using System.Globalization;

namespace Tweaker.Services;

public static class ServiceFormat
{
    public static string Status(ServiceState state) => state switch
    {
        ServiceState.Running => "Running",
        ServiceState.Stopped => "Stopped",
        ServiceState.StartPending => "Starting…",
        ServiceState.StopPending => "Stopping…",
        ServiceState.Paused => "Paused",
        _ => "Unknown",
    };

    public static string Startup(ServiceStartup startup) => startup switch
    {
        ServiceStartup.Automatic => "Automatic",
        ServiceStartup.AutomaticDelayed => "Automatic (Delayed)",
        ServiceStartup.Manual => "Manual",
        ServiceStartup.Disabled => "Disabled",
        ServiceStartup.Boot => "Boot",
        ServiceStartup.System => "System",
        _ => "—",
    };

    public static string Pid(ServiceState state, int pid) =>
        state == ServiceState.Running && pid > 0 ? pid.ToString(CultureInfo.InvariantCulture) : "—";
}
