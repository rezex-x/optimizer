using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class NetworkViewModelTests
{
    private static AdapterSample Nic(string name, long rx = 0, long tx = 0, bool up = true, bool virt = false,
        bool gw = true, string? ipv4 = "192.168.1.105", long speed = 1_000_000_000) =>
        new(name, name, name + " controller", ipv4, speed, up, virt, gw, rx, tx);

    private static NetworkSample At(double seconds, params AdapterSample[] adapters) =>
        new(TimeSpan.FromSeconds(seconds), adapters);

    private static ConnectionEntry Tcp(string process, int pid, string local, int localPort, string remote, int remotePort, TcpState state) =>
        new("TCP", IPAddress.Parse(local), localPort, IPAddress.Parse(remote), remotePort, state, pid, process);

    private static ConnectionEntry Udp(string process, int pid, string local, int localPort) =>
        new("UDP", IPAddress.Parse(local), localPort, null, null, null, pid, process);

    private static (NetworkViewModel vm, FakeNetworkAdapterSource adapters, FakeConnectionSource connections, FakeNetworkDiagnostic ping) Build()
    {
        var adapters = new FakeNetworkAdapterSource();
        var connections = new FakeConnectionSource();
        var ping = new FakeNetworkDiagnostic();
        return (new NetworkViewModel(adapters, connections, ping), adapters, connections, ping);
    }

    [Fact]
    public async Task Download_and_upload_follow_the_primary_adapter_in_mbps()
    {
        var (vm, adapters, _, _) = Build();
        adapters.Next = At(1, Nic("Ethernet"), Nic("vEthernet (WSL)", virt: true));
        await vm.RefreshAsync();
        Assert.Equal("—", vm.Download.ValueText);

        adapters.Next = At(2, Nic("Ethernet", rx: 1_250_000, tx: 125_000), Nic("vEthernet (WSL)", rx: 99_000_000, virt: true));
        await vm.RefreshAsync();

        Assert.Equal("10", vm.Download.ValueText);
        Assert.Equal("1.0", vm.Upload.ValueText);
        Assert.Equal("Ethernet · live while this tab is open", vm.Download.SubText);
        Assert.Equal("Ethernet", vm.Upload.SubText);
    }

    [Fact]
    public async Task With_no_adapter_up_the_cards_show_dashes_and_say_so()
    {
        var (vm, adapters, _, _) = Build();
        adapters.Next = At(1, Nic("Ethernet", up: false));
        await vm.RefreshAsync();
        adapters.Next = At(2, Nic("Ethernet", up: false));
        await vm.RefreshAsync();

        Assert.Equal("—", vm.Download.ValueText);
        Assert.Equal("No adapter connected", vm.Download.SubText);
    }

    [Fact]
    public async Task Coming_back_after_a_long_gap_starts_the_rate_fresh()
    {
        var (vm, adapters, _, _) = Build();
        adapters.Next = At(1, Nic("Ethernet", rx: 0));
        await vm.RefreshAsync();

        adapters.Next = At(300, Nic("Ethernet", rx: 3_000_000_000));
        await vm.RefreshAsync();

        Assert.Equal("—", vm.Download.ValueText);
    }

    [Fact]
    public async Task Empty_states_show_only_after_a_read_finds_nothing()
    {
        var (vm, _, connections, _) = Build();
        Assert.False(vm.ShowNoAdapters);
        Assert.False(vm.ShowNoConnections);

        await vm.RefreshAsync();

        Assert.True(vm.ShowNoAdapters);
        Assert.True(vm.ShowNoConnections);

        connections.Next = new[] { Tcp("chrome.exe", 4820, "192.168.1.105", 52341, "142.250.80.46", 443, TcpState.Established) };
        await vm.RefreshAsync();
        Assert.False(vm.ShowNoConnections);
    }

    [Fact]
    public async Task Adapters_list_up_physical_first_and_their_rows_are_reused()
    {
        var (vm, adapters, _, _) = Build();
        adapters.Next = At(1,
            Nic("Wi-Fi", up: false, ipv4: null),
            Nic("vEthernet (WSL)", virt: true, ipv4: "172.28.160.1", speed: 10_000_000_000),
            Nic("Ethernet"));
        await vm.RefreshAsync();

        Assert.Equal(new[] { "Ethernet", "vEthernet (WSL)", "Wi-Fi" }, vm.Adapters.Select(a => a.Name));
        AdapterRowViewModel ethernet = vm.Adapters[0];
        Assert.Equal("Ethernet controller", ethernet.Description);
        Assert.Equal("192.168.1.105", ethernet.Address);
        Assert.Equal("1 Gbps", ethernet.Speed);
        Assert.True(ethernet.IsUp);
        Assert.Equal("UP", ethernet.Status);
        Assert.Equal("10 Gbps", vm.Adapters[1].Speed);
        Assert.Equal("Not connected", vm.Adapters[2].Address);
        Assert.Equal("—", vm.Adapters[2].Speed);
        Assert.Equal("DOWN", vm.Adapters[2].Status);

        adapters.Next = At(2, Nic("Ethernet", up: false), Nic("vEthernet (WSL)", virt: true), Nic("Wi-Fi", up: false, ipv4: null));
        await vm.RefreshAsync();

        Assert.Same(ethernet, vm.Adapters.Single(a => a.Name == "Ethernet"));
        Assert.Equal("DOWN", ethernet.Status);
        Assert.Equal("vEthernet (WSL)", vm.Adapters[0].Name);
    }

    [Fact]
    public async Task Connections_are_sorted_by_process_under_a_count_header()
    {
        var (vm, _, connections, _) = Build();
        connections.Next = new[]
        {
            Tcp("steam.exe", 8820, "0.0.0.0", 27036, "0.0.0.0", 0, TcpState.Listen),
            Tcp("chrome.exe", 4820, "192.168.1.105", 52341, "142.250.80.46", 443, TcpState.Established),
            Udp("svchost.exe", 3128, "0.0.0.0", 5353),
        };

        await vm.RefreshAsync();

        Assert.Equal(new[] { "chrome.exe", "steam.exe", "svchost.exe" }, vm.Connections.Select(c => c.Process));
        Assert.Equal("ACTIVE CONNECTIONS (3)", vm.ConnectionsHeader);
        ConnectionRowViewModel chrome = vm.Connections[0];
        Assert.Equal(4820, chrome.Pid);
        Assert.Equal("192.168.1.105:52341", chrome.Local);
        Assert.Equal("142.250.80.46:443", chrome.Remote);
        Assert.Equal("ESTABLISHED", chrome.State);
        Assert.True(chrome.IsTcp);
        Assert.True(chrome.IsEstablished);
        Assert.True(vm.Connections[1].IsListening);
        ConnectionRowViewModel udp = vm.Connections[2];
        Assert.Equal("*:*", udp.Remote);
        Assert.Equal("—", udp.State);
        Assert.True(udp.IsStateless);
        Assert.False(udp.IsTcp);
    }

    [Fact]
    public async Task The_state_filter_shows_established_or_listening_only_without_a_new_read()
    {
        var (vm, _, connections, _) = Build();
        connections.Next = new[]
        {
            Tcp("steam.exe", 8820, "0.0.0.0", 27036, "0.0.0.0", 0, TcpState.Listen),
            Tcp("chrome.exe", 4820, "192.168.1.105", 52341, "142.250.80.46", 443, TcpState.Established),
            Tcp("OneDrive.exe", 2044, "192.168.1.105", 53021, "13.107.42.16", 443, TcpState.TimeWait),
            Udp("svchost.exe", 3128, "0.0.0.0", 5353),
        };
        await vm.RefreshAsync();

        vm.SetStateFilterCommand.Execute("Established");
        Assert.True(vm.IsEstablishedOnly);
        Assert.False(vm.IsAllStates);
        Assert.Equal(new[] { "chrome.exe" }, vm.Connections.Select(c => c.Process));

        vm.SetStateFilterCommand.Execute("Listening");
        Assert.True(vm.IsListeningOnly);
        Assert.Equal(new[] { "steam.exe" }, vm.Connections.Select(c => c.Process));
        Assert.Equal("ACTIVE CONNECTIONS (1)", vm.ConnectionsHeader);

        vm.SetStateFilterCommand.Execute("All");
        Assert.Equal(4, vm.Connections.Count);
        Assert.Equal(1, connections.Reads);
    }

    [Fact]
    public async Task The_process_filter_matches_names_case_insensitively_or_an_exact_pid()
    {
        var (vm, _, connections, _) = Build();
        connections.Next = new[]
        {
            Tcp("chrome.exe", 4820, "192.168.1.105", 52341, "142.250.80.46", 443, TcpState.Established),
            Tcp("Discord.exe", 6723, "192.168.1.105", 52400, "162.159.135.232", 443, TcpState.Established),
        };
        await vm.RefreshAsync();

        vm.ProcessFilter = "DISC";
        Assert.Equal(new[] { "Discord.exe" }, vm.Connections.Select(c => c.Process));

        vm.ProcessFilter = "4820";
        Assert.Equal(new[] { "chrome.exe" }, vm.Connections.Select(c => c.Process));

        vm.ProcessFilter = "";
        Assert.Equal(2, vm.Connections.Count);
    }

    [Fact]
    public async Task Rows_are_reused_across_refreshes_and_their_state_updates()
    {
        var (vm, _, connections, _) = Build();
        connections.Next = new[] { Tcp("chrome.exe", 4820, "192.168.1.105", 52341, "142.250.80.46", 443, TcpState.Established) };
        await vm.RefreshAsync();
        ConnectionRowViewModel row = vm.Connections[0];

        connections.Next = new[] { Tcp("chrome.exe", 4820, "192.168.1.105", 52341, "142.250.80.46", 443, TcpState.TimeWait) };
        await vm.RefreshAsync();

        Assert.Same(row, vm.Connections[0]);
        Assert.Equal("TIME_WAIT", row.State);
        Assert.True(row.IsOtherState);
        Assert.False(row.IsEstablished);
    }

    [Fact]
    public async Task Two_sockets_on_one_endpoint_get_their_own_rows()
    {
        var (vm, _, connections, _) = Build();
        connections.Next = new[] { Udp("svchost.exe", 3128, "0.0.0.0", 5353), Udp("svchost.exe", 3128, "0.0.0.0", 5353) };

        await vm.RefreshAsync();

        Assert.Equal(2, vm.Connections.Count);
        Assert.NotEqual(vm.Connections[0].Key, vm.Connections[1].Key);
    }

    [Fact]
    public async Task Ping_formats_both_targets_and_clears_the_running_flag()
    {
        var (vm, _, _, ping) = Build();
        ping.Next = new NetworkResult(
            new PingStat("1.1.1.1", 23.42, 20.0, 30.0, 2.14, 0.0),
            new PingStat("192.168.1.1", 1.2, 1.0, 2.0, 0.3, 0.0));

        await vm.RunPingAsync();

        Assert.Equal("23.4 ms", vm.InternetLatency);
        Assert.Equal("2.1 ms", vm.InternetJitter);
        Assert.Equal("0%", vm.InternetLoss);
        Assert.Equal("1.2 ms", vm.GatewayLatency);
        Assert.False(vm.IsPingRunning);
        Assert.Equal("Run test", vm.PingButtonLabel);
    }

    [Fact]
    public async Task An_empty_ping_result_after_a_full_one_resets_to_dashes()
    {
        var (vm, _, _, ping) = Build();
        ping.Next = new NetworkResult(
            new PingStat("1.1.1.1", 23.42, 20.0, 30.0, 2.14, 0.0),
            new PingStat("192.168.1.1", 1.2, 1.0, 2.0, 0.3, 0.0));
        await vm.RunPingAsync();

        ping.Next = NetworkResult.Empty;
        await vm.RunPingAsync();

        Assert.Equal("—", vm.InternetLatency);
        Assert.Equal("—", vm.GatewayLatency);
        Assert.Equal("—", vm.InternetLoss);
    }

    [Fact]
    public async Task Ping_sets_the_running_state_and_ignores_reentry()
    {
        var gated = new GatedNetworkDiagnostic();
        var vm = new NetworkViewModel(new FakeNetworkAdapterSource(), new FakeConnectionSource(), gated);

        Task first = vm.RunPingAsync();
        Assert.True(vm.IsPingRunning);
        Assert.Equal("Testing…", vm.PingButtonLabel);
        Assert.False(vm.RunPingCommand.CanExecute(null));

        await vm.RunPingAsync();
        Assert.Equal(1, gated.Calls);

        gated.Gate.SetResult(NetworkResult.Empty);
        await first;
        Assert.False(vm.IsPingRunning);
        Assert.Equal("Run test", vm.PingButtonLabel);
        Assert.True(vm.RunPingCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_tick_during_a_running_read_is_skipped()
    {
        var adapters = new GatedAdapterSource();
        var vm = new NetworkViewModel(adapters, new FakeConnectionSource(), new FakeNetworkDiagnostic());

        Task first = vm.RefreshAsync();
        Assert.True(adapters.Entered.Wait(TimeSpan.FromSeconds(5)));

        await vm.RefreshAsync();
        Assert.Equal(1, adapters.Reads);

        adapters.Release.Set();
        await first;
    }

    [Fact]
    public async Task A_failed_read_keeps_the_last_rows()
    {
        var connections = new FlakyConnectionSource
        {
            Next = new[] { Tcp("chrome.exe", 4820, "192.168.1.105", 52341, "142.250.80.46", 443, TcpState.Established) },
        };
        var vm = new NetworkViewModel(new FakeNetworkAdapterSource(), connections, new FakeNetworkDiagnostic());
        await vm.RefreshAsync();

        connections.Fail = true;
        await vm.RefreshAsync();

        Assert.Single(vm.Connections);
    }

    private sealed class GatedAdapterSource : INetworkAdapterSource
    {
        private int _reads;
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public int Reads => Volatile.Read(ref _reads);

        public NetworkSample Read()
        {
            Interlocked.Increment(ref _reads);
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(10));
            return NetworkSample.Empty;
        }
    }

    private sealed class FlakyConnectionSource : IConnectionSource
    {
        public bool Fail { get; set; }
        public IReadOnlyList<ConnectionEntry> Next { get; set; } = Array.Empty<ConnectionEntry>();
        public IReadOnlyList<ConnectionEntry> Read() => Fail ? throw new InvalidOperationException("table read failed") : Next;
    }
}
