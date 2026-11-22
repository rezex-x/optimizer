using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Tweaker.Services;

public sealed class NetworkInterfaceAdapterSource : INetworkAdapterSource
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public NetworkSample Read()
    {
        var adapters = new List<AdapterSample>();
        int? route = DefaultRouteIndex();
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                    string name = nic.Name, description = nic.Description;
                    if (NetworkRules.IsNoise(name, description)) continue;

                    IPInterfaceProperties props = nic.GetIPProperties();
                    string? ipv4 = props.UnicastAddresses
                        .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();
                    bool gateway = props.GatewayAddresses.Any(g =>
                        g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));

                    int? index = null;
                    try { index = props.GetIPv4Properties()?.Index; }
                    catch { }

                    long received = 0, sent = 0;
                    try
                    {
                        IPInterfaceStatistics stats = nic.GetIPStatistics();
                        received = stats.BytesReceived;
                        sent = stats.BytesSent;
                    }
                    catch { }

                    adapters.Add(new AdapterSample(nic.Id, name, description, ipv4, nic.Speed,
                        nic.OperationalStatus == OperationalStatus.Up, NetworkRules.IsVirtual(name, description),
                        gateway, received, sent, route is not null && index == route));
                }
                catch { }
            }
        }
        catch { }
        return new NetworkSample(_clock.Elapsed, adapters);
    }

    private static int? DefaultRouteIndex()
    {
        try
        {
            uint destination = BitConverter.ToUInt32(IPAddress.Parse("1.1.1.1").GetAddressBytes(), 0);
            return GetBestInterface(destination, out uint index) == 0 ? (int)index : null;
        }
        catch { return null; }
    }

    [DllImport("iphlpapi.dll")]
    private static extern uint GetBestInterface(uint destAddr, out uint bestIfIndex);
}
