using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;

namespace Tweaker.Services;

public sealed record RawSocket(string Protocol, IPAddress LocalAddress, int LocalPort,
    IPAddress? RemoteAddress, int? RemotePort, TcpState? State, int Pid);

public static class IpTableParser
{
    private const int Tcp4Row = 24, Tcp6Row = 56, Udp4Row = 12, Udp6Row = 28;

    public static List<RawSocket> ParseTcp4(ReadOnlySpan<byte> table)
    {
        int count = RowCount(table, Tcp4Row);
        var rows = new List<RawSocket>(count);
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> r = table.Slice(4 + i * Tcp4Row, Tcp4Row);
            rows.Add(new RawSocket("TCP",
                new IPAddress(r.Slice(4, 4)), Port(r.Slice(8, 4)),
                new IPAddress(r.Slice(12, 4)), Port(r.Slice(16, 4)),
                State(r.Slice(0, 4)), Pid(r.Slice(20, 4))));
        }
        return rows;
    }

    public static List<RawSocket> ParseTcp6(ReadOnlySpan<byte> table)
    {
        int count = RowCount(table, Tcp6Row);
        var rows = new List<RawSocket>(count);
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> r = table.Slice(4 + i * Tcp6Row, Tcp6Row);
            rows.Add(new RawSocket("TCP",
                V6(r.Slice(0, 16), r.Slice(16, 4)), Port(r.Slice(20, 4)),
                V6(r.Slice(24, 16), r.Slice(40, 4)), Port(r.Slice(44, 4)),
                State(r.Slice(48, 4)), Pid(r.Slice(52, 4))));
        }
        return rows;
    }

    public static List<RawSocket> ParseUdp4(ReadOnlySpan<byte> table)
    {
        int count = RowCount(table, Udp4Row);
        var rows = new List<RawSocket>(count);
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> r = table.Slice(4 + i * Udp4Row, Udp4Row);
            rows.Add(new RawSocket("UDP", new IPAddress(r.Slice(0, 4)), Port(r.Slice(4, 4)), null, null, null, Pid(r.Slice(8, 4))));
        }
        return rows;
    }

    public static List<RawSocket> ParseUdp6(ReadOnlySpan<byte> table)
    {
        int count = RowCount(table, Udp6Row);
        var rows = new List<RawSocket>(count);
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> r = table.Slice(4 + i * Udp6Row, Udp6Row);
            rows.Add(new RawSocket("UDP", V6(r.Slice(0, 16), r.Slice(16, 4)), Port(r.Slice(20, 4)), null, null, null, Pid(r.Slice(24, 4))));
        }
        return rows;
    }

    private static int RowCount(ReadOnlySpan<byte> table, int rowSize)
    {
        if (table.Length < 4) return 0;
        long declared = BinaryPrimitives.ReadUInt32LittleEndian(table);
        long fits = (table.Length - 4) / rowSize;
        return (int)Math.Min(declared, fits);
    }

    private static int Port(ReadOnlySpan<byte> dword) => (dword[0] << 8) | dword[1];

    private static int Pid(ReadOnlySpan<byte> dword) => (int)BinaryPrimitives.ReadUInt32LittleEndian(dword);

    private static TcpState State(ReadOnlySpan<byte> dword)
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(dword);
        return value is >= 1 and <= 12 ? (TcpState)value : TcpState.Unknown;
    }

    private static IPAddress V6(ReadOnlySpan<byte> address, ReadOnlySpan<byte> scope) =>
        new(address, BinaryPrimitives.ReadUInt32LittleEndian(scope));
}
