using System;
using System.Collections.Generic;
using System.Linq;

namespace Tweaker.Controls;

public readonly record struct SparkBar(double X, double Y, double Width, double Height, double Opacity);

public static class SparklineLayout
{
    public const double MinFraction = 0.03;

    public static IReadOnlyList<SparkBar> Compute(IReadOnlyList<double> values, double maximum, int slots,
        double width, double height, double gap = 1)
    {
        if (values.Count == 0 || slots <= 0 || width <= 0 || height <= 0) return Array.Empty<SparkBar>();

        double top = double.IsNaN(maximum) || maximum <= 0 ? values.Max() : maximum;
        if (!double.IsFinite(top) || top <= 0) top = 1;

        double pitch = (width + gap) / slots;
        if (pitch >= 2) pitch = Math.Floor(pitch);
        double barWidth = Math.Max(0, pitch - gap);
        double origin = width - (slots * pitch - gap);
        int shown = Math.Min(values.Count, slots);
        int firstValue = values.Count - shown;
        int firstSlot = slots - shown;

        var bars = new SparkBar[shown];
        for (int i = 0; i < shown; i++)
        {
            double v = values[firstValue + i];
            double fraction = double.IsFinite(v) ? Math.Clamp(v / top, MinFraction, 1) : MinFraction;
            double h = fraction * height;
            int slot = firstSlot + i;
            bars[i] = new SparkBar(origin + slot * pitch, height - h, barWidth, h, 0.4 + 0.6 * slot / slots);
        }
        return bars;
    }
}
