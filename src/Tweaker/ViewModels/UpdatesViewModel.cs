using System.Collections.ObjectModel;
using System.Windows.Input;
using Tweaker.Core.Catalog;
using Tweaker.Core.Services;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class PresetRow
{
    public PresetRow(UpdatePreset value, string label, string description, IReadOnlyList<string> details)
    {
        Value = value; Label = label; Description = description; Details = details;
    }
    public UpdatePreset Value { get; }
    public string Label { get; }
    public string Description { get; }
    public IReadOnlyList<string> Details { get; }
}

public sealed class UpdatesViewModel : ObservableObject
{
    private readonly IWindowsUpdateService _service;
    private readonly ActivityLog? _activity;
    private UpdatePreset _selected;
    private string _activeLabel = "";

    public UpdatesViewModel(IWindowsUpdateService service, ActivityLog? activity = null)
    {
        _service = service;
        _activity = activity;
        Log = activity?.Lines ?? new ObservableCollection<string>();
        Presets = new ObservableCollection<PresetRow>
        {
            new(UpdatePreset.Default, "Default (Microsoft)", "All updates enabled — feature + security + drivers",
                new[] { "Feature updates enabled", "Security updates enabled", "Automatic delivery on" }),
            new(UpdatePreset.SecurityOnly, "Security Only", "Receive security patches, defer feature updates",
                new[] { "Feature updates deferred 365 days", "Security updates enabled", "Quality updates on" }),
            new(UpdatePreset.DisableAll, "Disable All", "Block all Windows updates — manage manually",
                new[] { "Windows Update service disabled", "Feature + security updates blocked", "Use only when manually patching" }),
        };
        _selected = service.DetectActive();
        ApplyCommand = new RelayCommand(_ => Apply());
        UpdateActiveLabel(_selected);
    }

    public ObservableCollection<PresetRow> Presets { get; }

    public ObservableCollection<string> Log { get; }

    public ICommand ApplyCommand { get; }

    public UpdatePreset SelectedPreset
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                OnPropertyChanged(nameof(ShowWarning));
                OnPropertyChanged(nameof(SelectedRow));
            }
        }
    }

    public bool ShowWarning => _selected == UpdatePreset.DisableAll;

    public PresetRow? SelectedRow => Presets.FirstOrDefault(p => p.Value == _selected);

    public string ActiveLabel { get => _activeLabel; private set => SetField(ref _activeLabel, value); }

    private void Apply()
    {
        UpdateApplyResult r = _service.Apply(_selected);
        AddLog(r.Message);
        UpdateActiveLabel(_selected);
    }

    private void AddLog(string message)
    {
        if (_activity is not null) _activity.Add("Updates", message);
        else Log.Add($"{DateTime.Now:HH:mm:ss}  {message}");
    }

    private void UpdateActiveLabel(UpdatePreset p)
        => ActiveLabel = Presets.First(x => x.Value == p).Label + " is active";
}
