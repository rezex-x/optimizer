using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class AdapterRowViewModel : ObservableObject
{
    private string _name = "", _description = "", _address = "", _speed = "—";
    private bool _isUp;

    public AdapterRowViewModel(AdapterSample adapter)
    {
        Id = adapter.Id;
        Update(adapter);
    }

    public string Id { get; }
    public string Name { get => _name; private set => SetField(ref _name, value); }
    public string Description { get => _description; private set => SetField(ref _description, value); }
    public string Address { get => _address; private set => SetField(ref _address, value); }
    public string Speed { get => _speed; private set => SetField(ref _speed, value); }

    public bool IsUp
    {
        get => _isUp;
        private set { if (SetField(ref _isUp, value)) OnPropertyChanged(nameof(Status)); }
    }

    public string Status => _isUp ? "UP" : "DOWN";

    public void Update(AdapterSample adapter)
    {
        Name = adapter.Name;
        Description = adapter.Description;
        Address = NetworkFormat.Address(adapter.Ipv4);
        Speed = NetworkFormat.LinkSpeed(adapter.SpeedBps, adapter.IsUp);
        IsUp = adapter.IsUp;
    }
}

public sealed class ConnectionRowViewModel : ObservableObject
{
    private string _state;

    public ConnectionRowViewModel(string key, ConnectionEntry entry)
    {
        Key = key;
        Process = entry.ProcessName;
        Pid = entry.Pid;
        Protocol = entry.Protocol;
        Local = NetworkFormat.Endpoint(entry.LocalAddress, entry.LocalPort);
        Remote = NetworkFormat.Remote(entry.RemoteAddress, entry.RemotePort);
        _state = NetworkFormat.State(entry.State);
    }

    public string Key { get; }
    public string Process { get; }
    public int Pid { get; }
    public string Protocol { get; }
    public bool IsTcp => Protocol == "TCP";
    public string Local { get; }
    public string Remote { get; }

    public string State
    {
        get => _state;
        private set
        {
            if (!SetField(ref _state, value)) return;
            OnPropertyChanged(nameof(IsEstablished));
            OnPropertyChanged(nameof(IsListening));
            OnPropertyChanged(nameof(IsStateless));
            OnPropertyChanged(nameof(IsOtherState));
        }
    }

    public bool IsEstablished => _state == "ESTABLISHED";
    public bool IsListening => _state == "LISTENING";
    public bool IsStateless => _state == "—";
    public bool IsOtherState => !IsEstablished && !IsListening && !IsStateless;

    public void Update(ConnectionEntry entry) => State = NetworkFormat.State(entry.State);
}
