using System.Net;
using System.Net.NetworkInformation;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class NetworkFormatTests
{
    [Fact]
    public void Mbps_converts_bytes_per_second_to_megabits()
    {
        Assert.Equal(10.0, NetworkFormat.Mbps(1_250_000)!.Value, 6);
        Assert.Null(NetworkFormat.Mbps(null));
    }

    [Fact]
    public void Link_speed_reads_like_windows()
    {
        Assert.Equal("1 Gbps", NetworkFormat.LinkSpeed(1_000_000_000, true));
        Assert.Equal("2.5 Gbps", NetworkFormat.LinkSpeed(2_500_000_000, true));
        Assert.Equal("100 Mbps", NetworkFormat.LinkSpeed(100_000_000, true));
        Assert.Equal("—", NetworkFormat.LinkSpeed(-1, true));
        Assert.Equal("—", NetworkFormat.LinkSpeed(1_000_000_000, false));
    }

    [Fact]
    public void Address_without_ipv4_reads_not_connected()
    {
        Assert.Equal("Not connected", NetworkFormat.Address(null));
        Assert.Equal("192.168.1.105", NetworkFormat.Address("192.168.1.105"));
    }

    [Fact]
    public void Endpoints_bracket_ipv6()
    {
        Assert.Equal("192.168.1.5:443", NetworkFormat.Endpoint(IPAddress.Parse("192.168.1.5"), 443));
        Assert.Equal("[2a00:1450::200e]:443", NetworkFormat.Endpoint(IPAddress.Parse("2a00:1450::200e"), 443));
    }

    [Fact]
    public void A_missing_remote_end_reads_star()
    {
        Assert.Equal("*:*", NetworkFormat.Remote(null, null));
        Assert.Equal("1.1.1.1:53", NetworkFormat.Remote(IPAddress.Parse("1.1.1.1"), 53));
    }

    [Fact]
    public void States_use_netstat_names()
    {
        Assert.Equal("—", NetworkFormat.State(null));
        Assert.Equal("LISTENING", NetworkFormat.State(TcpState.Listen));
        Assert.Equal("ESTABLISHED", NetworkFormat.State(TcpState.Established));
        Assert.Equal("TIME_WAIT", NetworkFormat.State(TcpState.TimeWait));
        Assert.Equal("FIN_WAIT_2", NetworkFormat.State(TcpState.FinWait2));
        Assert.Equal("UNKNOWN", NetworkFormat.State(TcpState.Unknown));
    }
}
