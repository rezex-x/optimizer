using System.Windows.Input;

namespace Tweaker.ViewModels;

public enum ResourceTab { Profiles, Processes, Performance, Network, Services }

public sealed class ResourcesViewModel : ObservableObject, IDisposable
{
    private ResourceTab _selected = ResourceTab.Performance;

    public ResourcesViewModel(ProfilesViewModel profiles, PerformanceViewModel performance, NetworkViewModel network,
        ProcessesViewModel processes, ServicesViewModel services)
    {
        Profiles = profiles;
        Performance = performance;
        Network = network;
        Processes = processes;
        Services = services;
        SelectTabCommand = new RelayCommand(p => Select(p?.ToString()));
        profiles.Manager.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProfileManager.IsActive)) OnPropertyChanged(nameof(IsProfileActive));
        };
    }

    public ProfilesViewModel Profiles { get; }
    public PerformanceViewModel Performance { get; }
    public NetworkViewModel Network { get; }
    public ProcessesViewModel Processes { get; }
    public ServicesViewModel Services { get; }
    public ICommand SelectTabCommand { get; }

    public bool IsProfileActive => Profiles.Manager.IsActive;

    public ResourceTab Selected
    {
        get => _selected;
        private set
        {
            if (!SetField(ref _selected, value)) return;
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(IsProfiles));
            OnPropertyChanged(nameof(IsProcesses));
            OnPropertyChanged(nameof(IsPerformance));
            OnPropertyChanged(nameof(IsNetwork));
            OnPropertyChanged(nameof(IsServices));
        }
    }

    public object Current => _selected switch
    {
        ResourceTab.Profiles => Profiles,
        ResourceTab.Processes => Processes,
        ResourceTab.Network => Network,
        ResourceTab.Services => Services,
        _ => Performance,
    };

    public bool IsProfiles => _selected == ResourceTab.Profiles;
    public bool IsProcesses => _selected == ResourceTab.Processes;
    public bool IsPerformance => _selected == ResourceTab.Performance;
    public bool IsNetwork => _selected == ResourceTab.Network;
    public bool IsServices => _selected == ResourceTab.Services;

    private void Select(string? name)
    {
        if (Enum.TryParse<ResourceTab>(name, out var tab))
            Selected = tab;
    }

    public void Dispose()
    {
        Performance.Dispose();
        Processes.Dispose();
    }
}
