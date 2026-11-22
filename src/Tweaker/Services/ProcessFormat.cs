using System.Globalization;

namespace Tweaker.Services;

public enum UsageLevel { Normal, Warm, Hot }

public static class ProcessFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const double MiB = 1024.0 * 1024.0;

    public const double CpuWarm = 10, CpuHot = 30;
    public const double RamWarmMb = 800, RamHotMb = 3000;
    public const double DiskWarmMb = 1, DiskHotMb = 5;
    public const double GpuWarm = 10, GpuHot = 40;

    public static string Percent(double? value) => value is double v ? v.ToString("0.0", Inv) : "—";

    public static string Megabytes(long bytes) => (bytes / MiB).ToString("0", Inv);

    public static string MemoryPercent(long bytes, long totalBytes) =>
        totalBytes > 0 ? (bytes * 100.0 / totalBytes).ToString("0.0", Inv) : "—";

    public static string MegabytesPerSecond(double? bytesPerSec) =>
        bytesPerSec is double b ? (b / MiB).ToString("0.0", Inv) : "—";

    public static double ToMegabytes(double bytes) => bytes / MiB;

    public static UsageLevel Level(double? value, double warm, double hot) =>
        value is not double v ? UsageLevel.Normal : v >= hot ? UsageLevel.Hot : v >= warm ? UsageLevel.Warm : UsageLevel.Normal;
}
