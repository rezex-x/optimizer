using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Input;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public enum ConnectionStateFilter { All, Established, Listening }

public sealed class NetworkViewModel : ObservableObject
{
    private readonly INetworkAdapterSource _adapterSource;
    private readonly IConnectionSource _connectionSource;
    private readonly INetworkDiagnostic _ping;
    private NetworkSample? _previous;
    private IReadOnlyList<ConnectionEntry> _lastConnections = Array.Empty<ConnectionEntry>();
    private bool _refreshing;
    private bool _hasRead;
    private string _processFilter = "";
    private ConnectionStateFilter _stateFilter = ConnectionStateFilter.All;

    private string _internetLatency = "—", _internetJitter = "—", _internetLoss = "—";
    private string _gatewayLatency = "—", _gatewayJitter = "—", _gatewayLoss = "—";
    private string _pingButtonLabel = "Run test";
    private bool _isPingRunning;

    public NetworkViewModel(INetworkAdapterSource adapters, IConnectionSource connections, INetworkDiagnostic ping)
    {
        _adapterSource = adapters;
        _connectionSource = connections;
        _ping = ping;
        RunPingCommand = new RelayCommand(async _ => { await RunPingAsync(); CommandManager.InvalidateRequerySuggested(); },
            _ => !IsPingRunning);
        SetStateFilterCommand = new RelayCommand(p =>
        {
            if (Enum.TryParse(p?.ToString(), out ConnectionStateFilter filter)) StateFilter = filter;
        });
    }

    public MetricSeriesViewModel Download { get; } = new(fixedMax: null, autoFloor: 1);
    public MetricSeriesViewModel Upload { get; } = new(fixedMax: null, autoFloor: 1);

    public string InternetLatency { get => _internetLatency; private set => SetField(ref _internetLatency, value); }
    public string InternetJitter { get => _internetJitter; private set => SetField(ref _internetJitter, value); }
    public string InternetLoss { get => _internetLoss; private set => SetField(ref _internetLoss, value); }
    public string GatewayLatency { get => _gatewayLatency; private set => SetField(ref _gatewayLatency, value); }
    public string GatewayJitter { get => _gatewayJitter; private set => SetField(ref _gatewayJitter, value); }
    public string GatewayLoss { get => _gatewayLoss; private set => SetField(ref _gatewayLoss, value); }
    public string PingButtonLabel { get => _pingButtonLabel; private set => SetField(ref _pingButtonLabel, value); }
    public bool IsPingRunning { get => _isPingRunning; private set => SetField(ref _isPingRunning, value); }
    public ICommand RunPingCommand { get; }

    public ObservableCollection<AdapterRowViewModel> Adapters { get; } = new();
    public ObservableCollection<ConnectionRowViewModel> Connections { get; } = new();
    public string ConnectionsHeader => FormattableString.Invariant($"ACTIVE CONNECTIONS ({Connections.Count})");

    public bool ShowNoAdapters => _hasRead && Adapters.Count == 0;

    public bool ShowNoConnections => _hasRead && Connections.Count == 0;

    public string ProcessFilter
    {
        get => _processFilter;
        set { if (SetField(ref _processFilter, value ?? "")) ApplyConnections(); }
    }

    public ConnectionStateFilter StateFilter
    {
        get => _stateFilter;
        private set
        {
            if (!SetField(ref _stateFilter, value)) return;
            OnPropertyChanged(nameof(IsAllStates));
            OnPropertyChanged(nameof(IsEstablishedOnly));
            OnPropertyChanged(nameof(IsListeningOnly));
            ApplyConnections();
        }
    }

    public bool IsAllStates => _stateFilter == ConnectionStateFilter.All;
    public bool IsEstablishedOnly => _stateFilter == ConnectionStateFilter.Established;
    public bool IsListeningOnly => _stateFilter == ConnectionStateFilter.Listening;
    public ICommand SetStateFilterCommand { get; }

    public async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            (NetworkSample Sample, IReadOnlyList<ConnectionEntry> Connections) read;
            try
            {
                read = await Task.Run(() => (_adapterSource.Read(), _connectionSource.Read()));
            }
            catch
            {
                return;
            }

            _hasRead = true;
            ApplyThroughput(read.Sample);
            ApplyAdapters(read.Sample.Adapters);
            _lastConnections = read.Connections;
            ApplyConnections();
        }
        finally
        {
            _refreshing = false;
        }
    }

    public async Task RunPingAsync()
    {
        if (IsPingRunning) return;
        IsPingRunning = true;
        PingButtonLabel = "Testing…";
        try
        {
            NetworkResult r = await _ping.RunAsync();
            SetPing(r.Internet, s => InternetLatency = s, s => InternetJitter = s, s => InternetLoss = s);
            SetPing(r.Gateway, s => GatewayLatency = s, s => GatewayJitter = s, s => GatewayLoss = s);
        }
        finally
        {
            IsPingRunning = false;
            PingButtonLabel = "Run test";
        }
    }

    private static void SetPing(PingStat? p, Action<string> latency, Action<string> jitter, Action<string> loss)
    {
        latency(DiagnosticsFormat.Ms(p?.AvgMs));
        jitter(DiagnosticsFormat.Jitter(p?.JitterMs));
        loss(DiagnosticsFormat.Loss(p?.LossPct));
    }

    private void ApplyThroughput(NetworkSample sample)
    {
        if (_previous is not null && (sample.At - _previous.At).TotalSeconds > 3) _previous = null;

        AdapterSample? primary = NetworkRules.Primary(sample.Adapters);
        double? down = null, up = null;
        if (_previous is not null && primary is not null
            && NetworkRules.Rates(_previous, sample).TryGetValue(primary.Id, out var rate))
        {
            down = NetworkFormat.Mbps(rate.DownBps);
            up = NetworkFormat.Mbps(rate.UpBps);
        }
        _previous = sample;

        Download.Push(down);
        Upload.Push(up);
        Download.SubText = primary is null ? "No adapter connected" : primary.Name + " · live while this tab is open";
        Upload.SubText = primary?.Name ?? "";
    }

    private void ApplyAdapters(IReadOnlyList<AdapterSample> adapters)
    {
        List<AdapterSample> ordered = adapters
            .OrderByDescending(a => a.IsUp)
            .ThenBy(a => a.IsVirtual)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        KeyedSync.Apply(Adapters, ordered, a => a.Id, r => r.Id, a => new AdapterRowViewModel(a), (r, a) => r.Update(a));
        OnPropertyChanged(nameof(ShowNoAdapters));
    }

    private void ApplyConnections()
    {
        string filter = _processFilter.Trim();
        List<ConnectionEntry> visible = _lastConnections
            .Where(c => _stateFilter switch
            {
                ConnectionStateFilter.Established => c.State == TcpState.Established,
                ConnectionStateFilter.Listening => c.State == TcpState.Listen,
                _ => true,
            })
            .Where(c => filter.Length == 0
                || c.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || c.Pid.ToString(CultureInfo.InvariantCulture) == filter)
            .OrderBy(c => c.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Protocol, StringComparer.Ordinal)
            .ThenBy(c => c.LocalPort)
            .ThenBy(c => c.RemotePort ?? -1)
            .ThenBy(c => c.LocalAddress.ToString(), StringComparer.Ordinal)
            .ThenBy(c => c.RemoteAddress?.ToString() ?? "", StringComparer.Ordinal)
            .ToList();

        var keyed = new List<(string Key, ConnectionEntry Entry)>(visible.Count);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ConnectionEntry c in visible)
        {
            string key = c.Protocol + "|" + NetworkFormat.Endpoint(c.LocalAddress, c.LocalPort) + "|"
                + NetworkFormat.Remote(c.RemoteAddress, c.RemotePort) + "|" + c.Pid.ToString(CultureInfo.InvariantCulture);
            int repeat = seen.TryGetValue(key, out int count) ? count + 1 : 0;
            seen[key] = repeat;
            keyed.Add((repeat == 0 ? key : key + "#" + repeat.ToString(CultureInfo.InvariantCulture), c));
        }

        KeyedSync.Apply(Connections, keyed, k => k.Key, r => r.Key,
            k => new ConnectionRowViewModel(k.Key, k.Entry), (r, k) => r.Update(k.Entry));
        OnPropertyChanged(nameof(ConnectionsHeader));
        OnPropertyChanged(nameof(ShowNoConnections));
    }
}
