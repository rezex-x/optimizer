using System;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class NetworkRulesTests
{
    private static AdapterSample A(string name, bool up = true, bool virt = false, bool gw = true, long rx = 0, long tx = 0, bool route = false) =>
        new(name, name, name + " adapter", "192.168.1.5", 1_000_000_000, up, virt, gw, rx, tx, route);

    [Fact]
    public void Noise_adapters_are_wifi_direct_and_kernel_debug()
    {
        Assert.True(NetworkRules.IsNoise("Local Area Connection* 1", "Microsoft Wi-Fi Direct Virtual Adapter"));
        Assert.True(NetworkRules.IsNoise("Ethernet 3", "Microsoft Kernel Debug Network Adapter"));
        Assert.False(NetworkRules.IsNoise("Ethernet", "Intel(R) Ethernet Controller I225-V"));
    }

    [Fact]
    public void Hypervisor_and_vpn_adapters_are_virtual()
    {
        Assert.True(NetworkRules.IsVirtual("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter"));
        Assert.True(NetworkRules.IsVirtual("Ethernet 2", "TAP-Windows Adapter V9"));
        Assert.True(NetworkRules.IsVirtual("Tailscale", "Tailscale Tunnel"));
        Assert.False(NetworkRules.IsVirtual("Wi-Fi", "Intel(R) Wi-Fi 6E AX210 160MHz"));
        Assert.False(NetworkRules.IsVirtual("Ethernet", "Microsoft Hyper-V Network Adapter"));
    }

    [Fact]
    public void Primary_prefers_an_up_physical_adapter_with_a_gateway()
    {
        AdapterSample ethernet = A("Ethernet", rx: 10);
        AdapterSample wsl = A("vEthernet (WSL)", virt: true, rx: 9_000_000);
        AdapterSample wifiNoGateway = A("Wi-Fi", gw: false, rx: 5_000_000);
        Assert.Same(ethernet, NetworkRules.Primary(new[] { wsl, wifiNoGateway, ethernet }));
    }

    [Fact]
    public void Primary_prefers_the_adapter_windows_routes_the_internet_through()
    {
        AdapterSample wifi = A("Wi-Fi", rx: 9_000_000_000);
        AdapterSample ethernet = A("Ethernet", rx: 10, route: true);
        Assert.Same(ethernet, NetworkRules.Primary(new[] { wifi, ethernet }));
    }

    [Fact]
    public void Primary_picks_the_adapter_that_moved_the_most_data_within_a_tier()
    {
        AdapterSample ethernet = A("Ethernet", rx: 100);
        AdapterSample wifi = A("Wi-Fi", rx: 5_000, tx: 1);
        Assert.Same(wifi, NetworkRules.Primary(new[] { ethernet, wifi }));
    }

    [Fact]
    public void Primary_falls_back_to_physical_then_to_anything_up()
    {
        AdapterSample wifiNoGateway = A("Wi-Fi", gw: false);
        AdapterSample wsl = A("vEthernet (WSL)", virt: true);
        Assert.Same(wifiNoGateway, NetworkRules.Primary(new[] { wsl, wifiNoGateway }));
        Assert.Same(wsl, NetworkRules.Primary(new[] { wsl, A("Ethernet", up: false) }));
        Assert.Null(NetworkRules.Primary(new[] { A("Ethernet", up: false) }));
    }

    [Fact]
    public void Rates_divide_the_byte_deltas_by_the_elapsed_time()
    {
        var before = new NetworkSample(TimeSpan.FromSeconds(10), new[] { A("Ethernet", rx: 1_000, tx: 500) });
        var after = new NetworkSample(TimeSpan.FromSeconds(12), new[] { A("Ethernet", rx: 2_001_000, tx: 200_500) });
        var rate = NetworkRules.Rates(before, after)["Ethernet"];
        Assert.Equal(1_000_000, rate.DownBps, 6);
        Assert.Equal(100_000, rate.UpBps, 6);
    }

    [Fact]
    public void Rates_read_a_counter_reset_as_zero_and_skip_new_adapters()
    {
        var before = new NetworkSample(TimeSpan.FromSeconds(1), new[] { A("Ethernet", rx: 5_000, tx: 5_000) });
        var after = new NetworkSample(TimeSpan.FromSeconds(2), new[] { A("Ethernet", rx: 10, tx: 10), A("Wi-Fi", rx: 99) });
        var rates = NetworkRules.Rates(before, after);
        Assert.Equal((0.0, 0.0), rates["Ethernet"]);
        Assert.False(rates.ContainsKey("Wi-Fi"));
    }

    [Fact]
    public void Rates_are_empty_when_no_time_passed()
    {
        var s = new NetworkSample(TimeSpan.FromSeconds(1), new[] { A("Ethernet", rx: 1) });
        Assert.Empty(NetworkRules.Rates(s, s));
    }
}
