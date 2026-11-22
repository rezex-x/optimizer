using System.Collections.Generic;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeProcessSource : IProcessSource
{
    public ProcessSnapshot Next { get; set; } = ProcessSnapshot.Empty;
    public int Reads { get; private set; }
    public List<(int Pid, long Created)> Killed { get; } = new();
    public string? KillResult { get; set; }

    public ProcessSnapshot Read() { Reads++; return Next; }

    public string? Kill(int pid, long createdTicks)
    {
        Killed.Add((pid, createdTicks));
        return KillResult;
    }
}

public sealed class FakeGpuUsageSource : IGpuUsageSource
{
    public IReadOnlyDictionary<int, double> Next { get; set; } = new Dictionary<int, double>();
    public bool Disposed { get; private set; }
    public IReadOnlyDictionary<int, double> Read() => Next;
    public void Dispose() => Disposed = true;
}
