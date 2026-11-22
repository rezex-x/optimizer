using System;
using System.Collections.Generic;

namespace Tweaker.Services;

public enum ProcessKind { App, Background, Windows }

public sealed record ProcessSample(int Pid, long CreatedTicks, string Name, string? ImagePath, TimeSpan? CpuTime,
    long WorkingSetBytes, long? IoBytes, string Owner, bool HasWindow, bool IsCritical);

public sealed record ProcessSnapshot(TimeSpan At, IReadOnlyList<ProcessSample> Processes, long TotalMemoryBytes, int ProcessorCount)
{
    public static readonly ProcessSnapshot Empty = new(TimeSpan.Zero, Array.Empty<ProcessSample>(), 0, 1);
}

public interface IProcessSource
{
    ProcessSnapshot Read();

    string? Kill(int pid, long createdTicks);
}

public interface IGpuUsageSource : IDisposable
{
    IReadOnlyDictionary<int, double> Read();
}
