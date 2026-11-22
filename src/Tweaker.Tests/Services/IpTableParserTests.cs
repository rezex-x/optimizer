using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class IpTableParserTests
{
    private static byte[] Table(int rows, int rowSize, Action<byte[], int> fillRow, int declared = -1)
    {
        var buffer = new byte[4 + rows * rowSize];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)(declared < 0 ? rows : declared));
        for (int i = 0; i < rows; i++) fillRow(buffer, 4 + i * rowSize);
        return buffer;
    }

    private static void U32(byte[] b, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at, 4), value);
    private static void Port(byte[] b, int at, int port) { b[at] = (byte)(port >> 8); b[at + 1] = (byte)port; }
    private static void Ip(byte[] b, int at, string address) => IPAddress.Parse(address).GetAddressBytes().CopyTo(b, at);

    [Fact]
    public void Tcp4_rows_decode_state_endpoints_and_pid()
    {
        byte[] table = Table(1, 24, (b, at) =>
        {
            U32(b, at, 5);
            Ip(b, at + 4, "192.168.1.105"); Port(b, at + 8, 52341);
            Ip(b, at + 12, "142.250.80.46"); Port(b, at + 16, 443);
            U32(b, at + 20, 4820);
        });

        RawSocket s = Assert.Single(IpTableParser.ParseTcp4(table));

        Assert.Equal(new RawSocket("TCP", IPAddress.Parse("192.168.1.105"), 52341,
            IPAddress.Parse("142.250.80.46"), 443, TcpState.Established, 4820), s);
    }

    [Fact]
    public void Tcp4_listening_and_unknown_states()
    {
        byte[] table = Table(2, 24, (b, at) =>
        {
            U32(b, at, at == 4 ? 2u : 99u);
            Ip(b, at + 4, "0.0.0.0"); Port(b, at + 8, 27036);
            U32(b, at + 20, 8820);
        });

        var rows = IpTableParser.ParseTcp4(table);

        Assert.Equal(TcpState.Listen, rows[0].State);
        Assert.Equal(IPAddress.Any, rows[0].LocalAddress);
        Assert.Equal(0, rows[0].RemotePort);
        Assert.Equal(TcpState.Unknown, rows[1].State);
    }

    [Fact]
    public void Tcp6_rows_decode_addresses_ports_state_and_pid()
    {
        byte[] table = Table(1, 56, (b, at) =>
        {
            Ip(b, at, "2a01:4f8::1"); Port(b, at + 20, 50000);
            Ip(b, at + 24, "2a00:1450::200e"); Port(b, at + 44, 443);
            U32(b, at + 48, 5);
            U32(b, at + 52, 6723);
        });

        RawSocket s = Assert.Single(IpTableParser.ParseTcp6(table));

        Assert.Equal("TCP", s.Protocol);
        Assert.Equal(IPAddress.Parse("2a01:4f8::1"), s.LocalAddress);
        Assert.Equal(50000, s.LocalPort);
        Assert.Equal(IPAddress.Parse("2a00:1450::200e"), s.RemoteAddress);
        Assert.Equal(443, s.RemotePort);
        Assert.Equal(TcpState.Established, s.State);
        Assert.Equal(6723, s.Pid);
    }

    [Fact]
    public void Udp4_rows_have_no_remote_end_or_state()
    {
        byte[] table = Table(1, 12, (b, at) =>
        {
            Ip(b, at, "0.0.0.0"); Port(b, at + 4, 5353);
            U32(b, at + 8, 3128);
        });

        Assert.Equal(new RawSocket("UDP", IPAddress.Any, 5353, null, null, null, 3128),
            Assert.Single(IpTableParser.ParseUdp4(table)));
    }

    [Fact]
    public void Udp6_rows_decode_the_local_end()
    {
        byte[] table = Table(1, 28, (b, at) =>
        {
            Ip(b, at, "::"); Port(b, at + 20, 5353);
            U32(b, at + 24, 3128);
        });

        Assert.Equal(new RawSocket("UDP", IPAddress.IPv6Any, 5353, null, null, null, 3128),
            Assert.Single(IpTableParser.ParseUdp6(table)));
    }

    [Fact]
    public void A_truncated_or_tiny_buffer_yields_only_the_rows_that_fit()
    {
        byte[] table = Table(1, 12, (b, at) => U32(b, at + 8, 7), declared: 3);
        Assert.Single(IpTableParser.ParseUdp4(table));
        Assert.Empty(IpTableParser.ParseUdp4(new byte[] { 1, 0 }));
    }
}
