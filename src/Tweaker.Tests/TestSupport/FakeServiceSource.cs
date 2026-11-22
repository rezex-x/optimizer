using System.Collections.Generic;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeServiceSource : IServiceSource
{
    public ServiceSnapshot Next { get; set; } = ServiceSnapshot.Empty;
    public int Reads { get; private set; }
    public List<string> Started { get; } = new();
    public List<string> Stopped { get; } = new();
    public string? StartResult { get; set; }
    public string? StopResult { get; set; }

    public ServiceSnapshot Read() { Reads++; return Next; }
    public string? Start(string name) { Started.Add(name); return StartResult; }
    public string? Stop(string name) { Stopped.Add(name); return StopResult; }
}
