using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Tweaker.Services;

public enum MsiMode { Default, On, Off }

public sealed record AffinityChoice(string Label, int Policy, ulong Mask);

public static class InterruptRules
{
    public const int PolicyDefault = -1;
    public const int PolicySpecified = 4;
    public const int PolicySpread = 5;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string EnumRoot = @"SYSTEM\CurrentControlSet\Enum\";

    public static bool IsInterruptCapable(string instanceId) => instanceId.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase);

    public static string MsiPath(string instanceId) =>
        EnumRoot + instanceId + @"\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";

    public static string AffinityPath(string instanceId) =>
        EnumRoot + instanceId + @"\Device Parameters\Interrupt Management\Affinity Policy";

    public static IReadOnlyList<AffinityChoice> AffinityOptions(int processorCount)
    {
        var options = new List<AffinityChoice>
        {
            new("Windows default", PolicyDefault, 0),
            new("Spread across all cores", PolicySpread, 0),
        };
        int cpus = Math.Clamp(processorCount, 1, 64);
        for (int i = 0; i < cpus; i++) options.Add(new AffinityChoice("CPU " + i.ToString(Inv), PolicySpecified, 1UL << i));
        return options;
    }

    public static AffinityChoice Match(IReadOnlyList<AffinityChoice> options, int policy, ulong mask)
    {
        if (policy <= 0) return options[0];
        AffinityChoice? found = options.FirstOrDefault(o => o.Policy == policy && (policy != PolicySpecified || o.Mask == mask));
        return found ?? new AffinityChoice("Custom (policy " + policy.ToString(Inv) + ")", policy, mask);
    }

    public static byte[] MaskBytes(ulong mask) => BitConverter.GetBytes(mask);

    public static ulong MaskFrom(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return 0;
        var eight = new byte[8];
        Array.Copy(bytes, eight, Math.Min(8, bytes.Length));
        return BitConverter.ToUInt64(eight, 0);
    }

    public static string AffinityText(int policy, ulong mask) =>
        policy <= 0 ? "" : policy.ToString(Inv) + ":" + mask.ToString("X", Inv);

    public static (int Policy, ulong Mask) ParseAffinity(string text)
    {
        string[] parts = (text ?? "").Split(':');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, Inv, out int policy)
            && ulong.TryParse(parts[1], NumberStyles.HexNumber, Inv, out ulong mask))
            return (policy, mask);
        return (PolicyDefault, 0);
    }

    public static string MsiText(MsiMode mode) => mode switch { MsiMode.On => "1", MsiMode.Off => "0", _ => "" };

    public static MsiMode ParseMsi(string text) => text == "1" ? MsiMode.On : text == "0" ? MsiMode.Off : MsiMode.Default;
}
