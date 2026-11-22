using System;
using System.Collections.Generic;

namespace Tweaker.Core.Catalog;

public static class FreedCoverage
{
    private static readonly Dictionary<string, string[]> Covered = new(StringComparer.OrdinalIgnoreCase)
    {
        ["disable-sysmain"] = new[] { "SysMain" },
        ["disable-search-indexing"] = new[] { "WSearch" },
    };

    public static IReadOnlyList<string> ServicesFor(string tweakId) =>
        tweakId is not null && Covered.TryGetValue(tweakId, out string[]? services) ? services : Array.Empty<string>();
}
