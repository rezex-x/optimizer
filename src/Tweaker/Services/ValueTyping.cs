using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Tweaker.Services;

public enum EditorKind { Toggle, Slider, Choice, Text }

public sealed record ChoiceOption(string Value, string Label);

public sealed record EntryShape(EditorKind Kind, double Min = 0, double Max = 0, double Step = 1, bool IsFloat = false,
    IReadOnlyList<ChoiceOption>? Options = null, string Hint = "", bool Guessed = false);

public static class ValueTyping
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static EntryShape Infer(string value)
    {
        string v = value.Trim();
        if (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("false", StringComparison.OrdinalIgnoreCase))
            return new EntryShape(EditorKind.Toggle);

        if (long.TryParse(v, NumberStyles.AllowLeadingSign, Inv, out long whole))
        {
            if (Math.Abs((double)whole) > 1e9) return new EntryShape(EditorKind.Text);
            (double min, double max) = GuessRange(whole);
            return new EntryShape(EditorKind.Slider, min, max, 1, false, null, "range guessed from the value", true);
        }

        if (v.Contains('.') && double.TryParse(v, NumberStyles.Float, Inv, out double d) && double.IsFinite(d))
        {
            (double min, double max) = GuessRange(d);
            double step = max <= 1 ? 0.01 : max <= 10 ? 0.05 : max <= 100 ? 0.5 : 1;
            return new EntryShape(EditorKind.Slider, min, max, step, true, null, "range guessed from the value", true);
        }

        return new EntryShape(EditorKind.Text);
    }

    public static EntryShape ShapeFor(string key, string value, GamePreset? preset)
    {
        EntryShape? shape = preset?.ShapeFor(key);
        if (shape is null) return Infer(value);

        string v = value.Trim();
        if (shape.Kind == EditorKind.Slider) return shape with { IsFloat = v.Contains('.') };
        if (shape.Kind == EditorKind.Choice && shape.Options is { } options
            && !options.Any(o => string.Equals(o.Value, v, StringComparison.OrdinalIgnoreCase)))
            return shape with { Options = options.Append(new ChoiceOption(v, v + " (current)")).ToList() };
        return shape;
    }

    public static (double Min, double Max) GuessRange(double value)
    {
        double a = Math.Abs(value);
        double top = a <= 1 ? 1 : a <= 10 ? 10 : a <= 100 ? 100 : NiceCeil(a * 2);
        return value < 0 ? (-top, top) : (0, top);
    }

    public static double NiceCeil(double x)
    {
        if (x <= 0) return 1;
        double power = Math.Pow(10, Math.Floor(Math.Log10(x)));
        foreach (double m in new[] { 1d, 2d, 5d, 10d })
        {
            if (m * power >= x) return m * power;
        }
        return 10 * power;
    }

    public static string FormatBool(bool on, string original)
    {
        string o = original.Trim();
        string text = on ? "true" : "false";
        if (o.Length > 0 && o.All(c => !char.IsLetter(c) || char.IsLower(c))) return text;
        if (o.Length > 0 && o.All(c => !char.IsLetter(c) || char.IsUpper(c))) return text.ToUpperInvariant();
        return on ? "True" : "False";
    }

    public static string FormatNumber(double value, bool isFloat, string original, double step = 0)
    {
        if (!isFloat) return Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", Inv);
        string o = original.Trim();
        int dot = o.IndexOf('.');
        int decimals = dot >= 0 ? Math.Max(1, o.Length - dot - 1) : 1;
        decimals = Math.Max(decimals, DecimalsOf(step));
        return value.ToString("F" + decimals.ToString(Inv), Inv);
    }

    private static int DecimalsOf(double step)
    {
        if (!double.IsFinite(step) || step <= 0) return 0;
        string s = step.ToString("0.########", Inv);
        int dot = s.IndexOf('.');
        return dot >= 0 ? s.Length - dot - 1 : 0;
    }
}
