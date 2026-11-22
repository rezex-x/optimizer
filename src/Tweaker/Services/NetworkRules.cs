using System;
using System.Collections.Generic;
using System.Linq;

namespace Tweaker.Services;

public static class NetworkRules
{
    private static readonly string[] VirtualMarkers =
    {
        "Virtual", "VMware", "TAP-Windows", "WireGuard", "Wintun", "Tailscale", "ZeroTier", "OpenVPN",
    };

    public static bool IsNoise(string name, string description) =>
        name.StartsWith("Local Area Connection*", StringComparison.OrdinalIgnoreCase)
        || description.Contains("Wi-Fi Direct Virtual Adapter", StringComparison.OrdinalIgnoreCase)
        || description.Contains("Kernel Debug", StringComparison.OrdinalIgnoreCase);

    public static bool IsVirtual(string name, string description) =>
        name.StartsWith("vEthernet", StringComparison.OrdinalIgnoreCase)
        || VirtualMarkers.Any(m => description.Contains(m, StringComparison.OrdinalIgnoreCase));

    public static AdapterSample? Primary(IReadOnlyList<AdapterSample> adapters)
    {
        var tiers = new Func<AdapterSample, bool>[]
        {
            a => a.IsDefaultRoute,
            a => !a.IsVirtual && a.HasGateway,
            a => !a.IsVirtual,
            _ => true,
        };
        foreach (Func<AdapterSample, bool> tier in tiers)
        {
            AdapterSample? best = adapters.Where(a => a.IsUp).Where(tier)
                .OrderByDescending(a => a.BytesReceived + a.BytesSent)
                .FirstOrDefault();
            if (best is not null) return best;
        }
        return null;
    }

    public static IReadOnlyDictionary<string, (double DownBps, double UpBps)> Rates(NetworkSample previous, NetworkSample current)
    {
        var rates = new Dictionary<string, (double DownBps, double UpBps)>(StringComparer.Ordinal);
        double seconds = (current.At - previous.At).TotalSeconds;
        if (seconds <= 0) return rates;

        var before = new Dictionary<string, AdapterSample>(StringComparer.Ordinal);
        foreach (AdapterSample a in previous.Adapters) before.TryAdd(a.Id, a);

        foreach (AdapterSample a in current.Adapters)
        {
            if (!before.TryGetValue(a.Id, out AdapterSample? b)) continue;
            rates[a.Id] = (Math.Max(0, a.BytesReceived - b.BytesReceived) / seconds,
                           Math.Max(0, a.BytesSent - b.BytesSent) / seconds);
        }
        return rates;
    }
}
