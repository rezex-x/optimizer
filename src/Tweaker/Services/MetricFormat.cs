using System;
using System.Globalization;

namespace Tweaker.Services;

public static class MetricFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public const double BytesPerMiB = 1024.0 * 1024.0;

    public static string Load(double? v) => v is null ? "—" : v.Value.ToString("0", Inv) + "%";
    public static string Temp(double? v) => v is null ? "—" : v.Value.ToString("0", Inv) + "°C";
    public static string ClockGhz(double? mhz) => mhz is null ? "—" : (mhz.Value / 1000.0).ToString("0.00", Inv) + " GHz";

    public static string Value(double? v)
    {
        if (v is null) return "—";
        double oneDecimal = Math.Round(v.Value, 1, MidpointRounding.AwayFromZero);
        return oneDecimal < 10 ? oneDecimal.ToString("0.0", Inv) : v.Value.ToString("0", Inv);
    }
}
