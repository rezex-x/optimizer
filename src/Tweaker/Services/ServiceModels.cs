using System;
using System.Collections.Generic;

namespace Tweaker.Services;

public enum ServiceState { Unknown, Stopped, StartPending, StopPending, Running, Paused }

public enum ServiceStartup { Unknown, Automatic, AutomaticDelayed, Manual, Disabled, Boot, System }

public sealed record ServiceSample(string Name, string DisplayName, string Description, ServiceState State,
    ServiceStartup Startup, int Pid, bool IsWindows, bool AcceptsStop);

public sealed record ServiceSnapshot(IReadOnlyList<ServiceSample> Services)
{
    public static readonly ServiceSnapshot Empty = new(Array.Empty<ServiceSample>());
}

public interface IServiceSource
{
    ServiceSnapshot Read();

    string? Start(string name);

    string? Stop(string name);
}
