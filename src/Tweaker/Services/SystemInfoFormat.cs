using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Tweaker.Services;

public sealed record MemoryModule(ulong CapacityBytes, int? SpeedMts, int SmbiosType);

public static class SystemInfoFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const double BytesPerGiB = 1024.0 * 1024.0 * 1024.0;

    private static readonly (string Needle, string Short)[] BoardVendors =
    {
        ("Micro-Star", "MSI"), ("ASUSTeK", "ASUS"), ("Gigabyte", "Gigabyte"), ("ASRock", "ASRock"),
        ("Hewlett-Packard", "HP"), ("Dell", "Dell"), ("Lenovo", "Lenovo"),
    };

    public static string Cpu(string? name, double? baseMhz)
    {
        if (string.IsNullOrWhiteSpace(name)) return "—";
        string clean = Regex.Replace(name.Trim(), @"\s+", " ");
        if (clean.Contains('@') || baseMhz is not double mhz || mhz <= 0) return clean;
        return clean + " @ " + (mhz / 1000.0).ToString("0.0", Inv) + " GHz";
    }

    public static string CoresThreads(int? cores, int? threads) =>
        cores is null && threads is null ? "—" : Num(cores) + " / " + Num(threads);

    public static string? MemoryType(int smbiosType) => smbiosType switch
    {
        24 => "DDR3",
        26 => "DDR4",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _ => null,
    };

    public static string Memory(IReadOnlyList<MemoryModule> modules)
    {
        List<double> sizes = modules.Where(m => m.CapacityBytes > 0).Select(m => m.CapacityBytes / BytesPerGiB).ToList();
        if (sizes.Count == 0) return "—";

        string? type = modules.Select(m => MemoryType(m.SmbiosType)).FirstOrDefault(t => t is not null);
        int? speed = modules.Where(m => m.SpeedMts is > 0).Select(m => m.SpeedMts).Max();

        string kind = "";
        if (type is not null && speed is int s1) kind = " " + type + "-" + s1.ToString(Inv);
        else if (type is not null) kind = " " + type;
        else if (speed is int s2) kind = " " + s2.ToString(Inv) + " MT/s";

        string layout = sizes.Distinct().Count() == 1
            ? sizes.Count.ToString(Inv) + " × " + Gb(sizes[0])
            : string.Join(" + ", sizes.OrderByDescending(s => s).Select(Gb));
        return Gb(sizes.Sum()) + kind + " (" + layout + ")";
    }

    public static string Board(string? maker, string? product)
    {
        string? m = Clean(maker), p = Clean(product);
        if (m is not null)
        {
            foreach (var (needle, shortName) in BoardVendors)
            {
                if (m.Contains(needle, StringComparison.OrdinalIgnoreCase)) { m = shortName; break; }
            }
        }
        if (m is null && p is null) return "—";
        if (m is null) return p!;
        if (p is null) return m;
        return p.StartsWith(m, StringComparison.OrdinalIgnoreCase) ? p : m + " " + p;
    }

    public static string Os(string? productName, string? displayVersion, int? build)
    {
        if (string.IsNullOrWhiteSpace(productName)) return "—";
        string name = productName.Trim();
        if (build >= 22000 && name.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
            name = "Windows 11" + name.Substring("Windows 10".Length);
        if (!string.IsNullOrWhiteSpace(displayVersion)) name += " " + displayVersion.Trim();
        if (build is int b) name += " (" + b.ToString(Inv) + ")";
        return name;
    }

    public static string Uptime(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        string h = t.Hours.ToString(Inv), m = t.Minutes.ToString(Inv);
        if (t.TotalDays >= 1) return ((int)t.TotalDays).ToString(Inv) + "d " + h + "h " + m + "m";
        if (t.TotalHours >= 1) return h + "h " + m + "m";
        return m + "m";
    }

    public static string Processes(int? count) => count is int n ? n.ToString(Inv) + " running" : "—";

    public static string GpuNames(IReadOnlyList<string> names)
    {
        List<string> list = names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return list.Count == 0 ? "—" : string.Join(" + ", list);
    }

    private static string Num(int? v) => v?.ToString(Inv) ?? "—";

    private static string Gb(double gb) => gb.ToString("0.#", Inv) + " GB";

    private static string? Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string t = s.Trim();
        bool placeholder = t.Contains("O.E.M", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Default string", StringComparison.OrdinalIgnoreCase)
            || t.Equals("System Product Name", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Not Applicable", StringComparison.OrdinalIgnoreCase);
        return placeholder ? null : t;
    }
}
