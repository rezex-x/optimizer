using System;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeSystemInfoSource : ISystemInfoSource
{
    public SystemInfo Info { get; set; } = SystemInfo.Empty;
    public int? Processes { get; set; }
    public TimeSpan UptimeValue { get; set; } = TimeSpan.Zero;
    public int ReadCalls { get; private set; }

    public SystemInfo Read() { ReadCalls++; return Info; }
    public int? ProcessCount() => Processes;
    public TimeSpan Uptime() => UptimeValue;
}
