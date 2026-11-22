using System.Globalization;

namespace Tweaker.Services;

public static class DiagnosticsFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Ms(double? v) => v is null ? "—" : v.Value.ToString("0.0", Inv) + " ms";
    public static string Jitter(double? v) => v is null ? "—" : v.Value.ToString("0.0", Inv) + " ms";
    public static string Loss(double? pct) => pct is null ? "—" : pct.Value.ToString("0.#", Inv) + "%";
    public static string Pct(double? v) =>
        v is null ? "—"
        : v.Value <= 0 ? "0%"
        : v.Value < 1 ? "<1%"
        : v.Value.ToString("0", Inv) + "%";
    public static string Rate(double? v) => v is null ? "—" : v.Value.ToString("0", Inv) + " /s";
    public static string Gb(double? v) => v is null ? "—" : v.Value.ToString("0.0", Inv) + " GB";

    public static string UsedOfTotalGb(double? free, double? size) =>
        (free is null || size is null) ? "—"
        : (size.Value - free.Value).ToString("0.0", Inv) + " / " + size.Value.ToString("0.0", Inv) + " GB";

    public static string Kind(DriveKind k) => k switch { DriveKind.Ssd => "SSD", DriveKind.Hdd => "HDD", _ => "—" };
    public static string Smart(SmartHealth h) => h switch
    {
        SmartHealth.Ok => "Healthy",
        SmartHealth.PredictedFail => "Predicted failure",
        _ => "Unknown",
    };
}
