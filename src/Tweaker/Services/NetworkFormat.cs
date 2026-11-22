using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Tweaker.Services;

public static class NetworkFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static double? Mbps(double? bytesPerSec) => bytesPerSec * 8 / 1_000_000;

    public static string LinkSpeed(long bps, bool isUp)
    {
        if (!isUp || bps <= 0) return "—";
        if (bps >= 1_000_000_000) return (bps / 1e9).ToString("0.#", Inv) + " Gbps";
        if (bps >= 1_000_000) return (bps / 1e6).ToString("0", Inv) + " Mbps";
        return (bps / 1e3).ToString("0", Inv) + " Kbps";
    }

    public static string Address(string? ipv4) => string.IsNullOrWhiteSpace(ipv4) ? "Not connected" : ipv4;

    public static string Endpoint(IPAddress address, int port) =>
        (address.AddressFamily == AddressFamily.InterNetworkV6 ? "[" + address + "]" : address.ToString())
        + ":" + port.ToString(Inv);

    public static string Remote(IPAddress? address, int? port) =>
        address is null || port is null ? "*:*" : Endpoint(address, port.Value);

    public static string State(TcpState? state) => state switch
    {
        null => "—",
        TcpState.Listen => "LISTENING",
        TcpState.Established => "ESTABLISHED",
        TcpState.SynSent => "SYN_SENT",
        TcpState.SynReceived => "SYN_RECEIVED",
        TcpState.FinWait1 => "FIN_WAIT_1",
        TcpState.FinWait2 => "FIN_WAIT_2",
        TcpState.CloseWait => "CLOSE_WAIT",
        TcpState.Closing => "CLOSING",
        TcpState.LastAck => "LAST_ACK",
        TcpState.TimeWait => "TIME_WAIT",
        TcpState.DeleteTcb => "DELETE_TCB",
        TcpState.Closed => "CLOSED",
        _ => "UNKNOWN",
    };
}
