using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Tweaker.Services;

public sealed class PingNetworkDiagnostic : INetworkDiagnostic
{
    private const int Attempts = 20;
    private const int TimeoutMs = 1000;

    public Task<NetworkResult> RunAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        try
        {
            PingStat? internet = Measure("1.1.1.1", ct);
            IPAddress? gw = DefaultGateway();
            PingStat? gateway = gw is null ? null : Measure(gw.ToString(), ct);
            return new NetworkResult(internet, gateway);
        }
        catch { return NetworkResult.Empty; }
    });

    private static PingStat? Measure(string target, CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();
            var rtts = new List<double>();
            int fails = 0;
            for (int i = 0; i < Attempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    PingReply reply = ping.Send(target, TimeoutMs);
                    if (reply.Status == IPStatus.Success) rtts.Add(reply.RoundtripTime);
                    else fails++;
                }
                catch { fails++; }
            }
            double loss = 100.0 * fails / Attempts;
            if (rtts.Count == 0) return new PingStat(target, null, null, null, null, loss);

            double avg = rtts.Average(), min = rtts.Min(), max = rtts.Max();
            double? jitter = null;
            if (rtts.Count > 1)
            {
                double sum = 0;
                for (int i = 1; i < rtts.Count; i++) sum += Math.Abs(rtts[i] - rtts[i - 1]);
                jitter = sum / (rtts.Count - 1);
            }
            return new PingStat(target, avg, min, max, jitter, loss);
        }
        catch { return null; }
    }

    private static IPAddress? DefaultGateway()
    {
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (GatewayIPAddressInformation g in ni.GetIPProperties().GatewayAddresses)
                        if (g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))
                            return g.Address;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
}
