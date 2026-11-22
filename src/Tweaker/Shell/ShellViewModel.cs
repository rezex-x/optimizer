using System.Collections.ObjectModel;
using System.Windows.Input;
using Tweaker.Services;
using Tweaker.ViewModels;

namespace Tweaker.Shell;

public sealed class ShellViewModel : ObservableObject
{
    private Tab _selected = Tab.Tweaks;
    private readonly GpuInfo _gpu;

    public ShellViewModel(MainViewModel tweaks, InstallViewModel install, ConfigViewModel config, UpdatesViewModel updates,
        NvidiaViewModel nvidia, GpuInfo gpu, ResourcesViewModel resources, HardwareViewModel hardware, InspectorViewModel inspector,
        PendingChangesViewModel? pending = null, ActivityLog? activity = null, FreedResourcesViewModel? freed = null)
    {
        Tweaks = tweaks;
        Install = install;
        Config = config;
        Updates = updates;
        Nvidia = nvidia;
        _gpu = gpu;
        Resources = resources;
        Hardware = hardware;
        Inspector = inspector;
        Pending = pending ?? new PendingChangesViewModel();
        ActivityLines = activity?.Lines ?? new ObservableCollection<string>();
        Freed = freed ?? new FreedResourcesViewModel();
        SelectTabCommand = new RelayCommand(p => Select(p?.ToString()));
        resources.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ResourcesViewModel.IsProfileActive)) OnPropertyChanged(nameof(IsProfileActive));
            if (e.PropertyName == nameof(ResourcesViewModel.Selected)) OnPropertyChanged(nameof(Tip));
        };
    }

    public MainViewModel Tweaks { get; }
    public InstallViewModel Install { get; }
    public ConfigViewModel Config { get; }
    public UpdatesViewModel Updates { get; }
    public NvidiaViewModel Nvidia { get; }
    public ResourcesViewModel Resources { get; }
    public HardwareViewModel Hardware { get; }
    public InspectorViewModel Inspector { get; }

    public PendingChangesViewModel Pending { get; }

    public ObservableCollection<string> ActivityLines { get; }

    public FreedResourcesViewModel Freed { get; }

    public string Tip => TabTips.For(_selected, _selected == Tab.Resources ? Resources.Selected : null);

    public ICommand SelectTabCommand { get; }

    public Tab Selected
    {
        get => _selected;
        private set
        {
            if (!SetField(ref _selected, value)) return;
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(IsInstall));
            OnPropertyChanged(nameof(IsTweaks));
            OnPropertyChanged(nameof(IsConfig));
            OnPropertyChanged(nameof(IsUpdates));
            OnPropertyChanged(nameof(IsNvidia));
            OnPropertyChanged(nameof(IsResources));
            OnPropertyChanged(nameof(IsHardware));
            OnPropertyChanged(nameof(IsInspector));
            OnPropertyChanged(nameof(Tip));
        }
    }

    public object Current => _selected switch
    {
        Tab.Install => Install,
        Tab.Config => Config,
        Tab.Updates => Updates,
        Tab.Nvidia => Nvidia,
        Tab.Resources => Resources,
        Tab.Hardware => Hardware,
        Tab.Inspector => Inspector,
        _ => Tweaks,
    };

    public bool IsInstall => _selected == Tab.Install;
    public bool IsTweaks => _selected == Tab.Tweaks;
    public bool IsConfig => _selected == Tab.Config;
    public bool IsUpdates => _selected == Tab.Updates;
    public bool IsNvidia => _selected == Tab.Nvidia;
    public bool IsResources => _selected == Tab.Resources;
    public bool IsHardware => _selected == Tab.Hardware;
    public bool IsInspector => _selected == Tab.Inspector;

    public bool IsNvidiaVisible => _gpu.HasNvidia;

    public bool IsProfileActive => Resources.IsProfileActive;

    private void Select(string? name)
    {
        if (Enum.TryParse<Tab>(name, out var tab))
            Selected = tab;
    }
}
