using System;
using System.Collections.Generic;

namespace Tweaker.Services;

public sealed record SystemInfo(
    string? CpuName, double? CpuBaseMhz, int? Cores, int? Threads,
    IReadOnlyList<MemoryModule> Memory, IReadOnlyList<string> Gpus,
    string? BoardMaker, string? BoardProduct,
    string? OsName, string? OsVersion, int? OsBuild)
{
    public static readonly SystemInfo Empty = new(null, null, null, null,
        Array.Empty<MemoryModule>(), Array.Empty<string>(), null, null, null, null, null);
}

public interface ISystemInfoSource
{
    SystemInfo Read();

    int? ProcessCount();

    TimeSpan Uptime();
}
