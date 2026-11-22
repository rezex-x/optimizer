using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;

namespace Tweaker.Services;

public sealed record AdapterSample(string Id, string Name, string Description, string? Ipv4, long SpeedBps,
    bool IsUp, bool IsVirtual, bool HasGateway, long BytesReceived, long BytesSent, bool IsDefaultRoute = false);

public sealed record NetworkSample(TimeSpan At, IReadOnlyList<AdapterSample> Adapters)
{
    public static readonly NetworkSample Empty = new(TimeSpan.Zero, Array.Empty<AdapterSample>());
}

public interface INetworkAdapterSource
{
    NetworkSample Read();
}

public sealed record ConnectionEntry(string Protocol, IPAddress LocalAddress, int LocalPort,
    IPAddress? RemoteAddress, int? RemotePort, TcpState? State, int Pid, string ProcessName);

public interface IConnectionSource
{
    IReadOnlyList<ConnectionEntry> Read();
}
