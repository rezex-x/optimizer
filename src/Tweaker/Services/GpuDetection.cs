using System;
using System.Collections.Generic;
using System.Linq;

namespace Tweaker.Services;

public sealed record GpuInfo(bool HasNvidia, bool HasAmd, bool HasIntel, IReadOnlyList<string> Names);

public interface IGpuDetector
{
    GpuInfo Detect();
}

public static class GpuVendors
{
    public static GpuInfo From(IEnumerable<string> names)
    {
        var list = names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToList();
        bool Has(string needle) => list.Any(n => n.Contains(needle, StringComparison.OrdinalIgnoreCase));
        return new GpuInfo(Has("NVIDIA"), Has("AMD") || Has("Radeon"), Has("Intel"), list);
    }
}
