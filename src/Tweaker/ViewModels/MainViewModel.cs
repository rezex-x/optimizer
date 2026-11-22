using System.Collections.ObjectModel;
using System.ComponentModel;
using Tweaker.Core.Catalog;
using Tweaker.Core.Models;
using Tweaker.Core.Services;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public record TweakPreset(string Name, string[] Ids);

public sealed class MainViewModel : ObservableObject
{
    public const string AllCategory = "All";

    private readonly CatalogService _catalog;
    private readonly ITweakEngine _engine;
    private readonly IConfirmationService _confirm;
    private readonly IPowerSettings? _power;
    private readonly IPowerBackup? _powerBackup;
    private readonly PendingChangesViewModel? _pending;
    private readonly ActivityLog? _activity;
    private readonly IAppxService? _appx;
    private string _selectedCategory = AllCategory;
    private string _activeCategory = AllCategory;
    private string _searchText = "";
    private readonly Dictionary<string, TweakViewModel> _rows = new(StringComparer.Ordinal);

    public MainViewModel(CatalogService catalog, ITweakEngine engine, IConfirmationService confirm,
        IPowerSettings? power = null, IPowerBackup? powerBackup = null,
        PendingChangesViewModel? pending = null, ActivityLog? activity = null, IAppxService? appx = null)
    {
        _catalog = catalog;
        _engine = engine;
        _confirm = confirm;
        _power = power;
        _powerBackup = powerBackup;
        _pending = pending;
        _activity = activity;
        _appx = appx;
        Log = activity?.Lines ?? new ObservableCollection<string>();

        Categories = new ObservableCollection<string> { AllCategory };
        foreach (string category in _catalog.Categories())
            if (!ConfigSurface.IsConfig(category))
                Categories.Add(category);

        IsDev = catalog.CanEdit;
        Rebuild();
    }

    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<CategoryGroup> Groups { get; } = new();

    public ObservableCollection<string> Log { get; }

    public bool IsDev { get; }

    public IReadOnlyList<TweakPreset> Presets { get; } = new[]
    {
        new TweakPreset("Recommended", new[]
        {
            "disable-gamedvr", "disable-fso", "game-mode-on", "disable-mouse-accel",
            "disable-sticky-filter-keys", "disable-advertising-id", "disable-app-suggestions",
            "disable-startup-delay",
        }),
        new TweakPreset("Performance", new[]
        {
            "disable-gamedvr", "disable-fso", "gpu-hags", "visualfx-perf", "priority-separation",
            "system-responsiveness", "network-throttling-off", "disable-sysmain",
            "disable-core-parking", "ultimate-power", "mpo-off",
        }),
        new TweakPreset("Competitive", new[]
        {
            "disable-gamedvr", "disable-fso", "gpu-hags", "visualfx-perf", "priority-separation",
            "system-responsiveness", "network-throttling-off", "disable-nagle", "disable-mouse-accel",
            "disable-core-parking", "ultimate-power", "menu-show-delay", "game-mode-on",
        }),
        new TweakPreset("Streaming", new[]
        {
            "disable-gamedvr", "disable-fso", "gpu-hags", "visualfx-perf", "system-responsiveness",
            "network-throttling-off", "disable-startup-delay", "ultimate-power",
        }),
        new TweakPreset("Privacy & Debloat", new[]
        {
            "disable-telemetry", "disable-advertising-id", "disable-cortana", "disable-app-suggestions",
            "disable-activity-history", "consumerfeatures-off", "location-tracking-off",
            "background-apps-off", "windows-ai-off", "delivery-optimization-off", "store-search-off",
            "bing-search-off",
        }),
        new TweakPreset("Balanced", new[]
        {
            "disable-gamedvr", "game-mode-on", "disable-mouse-accel", "disable-advertising-id",
            "disable-app-suggestions", "disable-startup-delay", "disable-sticky-filter-keys",
        }),
    };

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (SetField(ref _selectedCategory, value)) { ActiveCategory = value; Rebuild(); } }
    }

    public string ActiveCategory
    {
        get => _activeCategory;
        set => SetField(ref _activeCategory, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value)) return;
            Rebuild();
            OnPropertyChanged(nameof(MythNote));
            OnPropertyChanged(nameof(ShowMythNote));
        }
    }

    public int EstimatedFreedMb => AppliedEstimateTweaks().Sum(t => t.FreesRamMb!.Value);

    public IReadOnlySet<string> CoveredServices => AppliedEstimateTweaks()
        .SelectMany(t => FreedCoverage.ServicesFor(t.Id))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private IEnumerable<Tweak> AppliedEstimateTweaks() => _catalog.Tweaks
        .Where(t => t.Type != TweakType.Value && t.FreesRamMb is > 0)
        .Where(t => _engine.GetState(t) == TweakState.Applied);

    public string MythNote => MythNotes.For(_searchText) ?? "";
    public bool ShowMythNote => MythNote.Length > 0;

    private void Rebuild()
    {
        Groups.Clear();

        IEnumerable<string> categories = SelectedCategory == AllCategory
            ? _catalog.Categories().Where(c => !ConfigSurface.IsConfig(c))
            : new[] { SelectedCategory };

        foreach (string category in categories)
        {
            IEnumerable<Tweak> tweaks = _catalog.InCategory(category);
            if (_power is null || _powerBackup is null)
                tweaks = tweaks.Where(t => t.Type != TweakType.Value);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string term = SearchText.Trim();
                tweaks = tweaks.Where(t =>
                    t.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var vms = tweaks.Select(RowFor).ToList();
            if (vms.Count > 0)
                Groups.Add(new CategoryGroup(category, vms));
        }
    }

    private TweakViewModel RowFor(Tweak tweak)
    {
        if (!_rows.TryGetValue(tweak.Id, out TweakViewModel? row))
        {
            PowerValueViewModel? value = tweak.Type == TweakType.Value && _power is not null && _powerBackup is not null
                ? new PowerValueViewModel(tweak, _power, _powerBackup, _confirm, AddLog, _pending)
                : null;
            AppRemovalViewModel? apps = _appx is not null && tweak.Id == RemovableApps.TweakId
                ? new AppRemovalViewModel(_appx, AddLog)
                : null;
            row = new TweakViewModel(tweak, _engine, _confirm, AddLog, value, RefreshValueRows, _pending, apps);
            row.PropertyChanged += OnRowChanged;
            if (apps is not null) _ = apps.LoadAsync();
            _rows[tweak.Id] = row;
        }
        return row;
    }

    private void RefreshValueRows()
    {
        foreach (TweakViewModel row in _rows.Values)
            if (row.IsValue && row.Value is not null) row.Value.Refresh();
    }

    public void RefreshStates()
    {
        foreach (TweakViewModel row in _rows.Values) row.RefreshState();
        OnPropertyChanged(nameof(EstimatedFreedMb));
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TweakViewModel.IsApplied))
            OnPropertyChanged(nameof(EstimatedFreedMb));
    }

    private void AddLog(string message)
    {
        if (_activity is not null) _activity.Add("Tweaks", message);
        else Log.Add($"{DateTime.Now:HH:mm:ss}  {message}");
    }

    public void AddTweak(Tweak tweak)
    {
        _catalog.AddOrUpdate(tweak);
        _rows.Remove(tweak.Id);
        _pending?.Unstage("tweak|" + tweak.Id);
        _pending?.Unstage("power|" + tweak.Id);

        foreach (string category in _catalog.Categories())
            if (!ConfigSurface.IsConfig(category) && !Categories.Contains(category))
                Categories.Add(category);

        Rebuild();
    }

    public void ApplyPreset(TweakPreset preset)
    {
        var resolved = preset.Ids
            .Select(id => _catalog.Tweaks.FirstOrDefault(t => t.Id == id))
            .Where(t => t is not null && t.Type != TweakType.Value).Cast<Tweak>().ToList();
        if (resolved.Count == 0) return;

        if (_pending is not null)
        {
            if (_pending.IsApplying)
            {
                AddLog($"Preset {preset.Name}: wait for the current Apply to finish");
                return;
            }
            if (_pending.HasItems) _pending.DiscardAll();
        }

        var toApply = new List<Tweak>();
        foreach (Tweak tweak in resolved)
        {
            if (_rows.TryGetValue(tweak.Id, out TweakViewModel? row) && row.IsBusy)
                AddLog($"Preset {preset.Name}: skipped {tweak.Name} (still running)");
            else if (_engine.GetState(tweak) == TweakState.Applied)
                AddLog($"Preset {preset.Name}: {tweak.Name} already applied");
            else
                toApply.Add(tweak);
        }
        if (toApply.Count == 0) return;

        if (_pending is not null)
        {
            int queued = 0;
            foreach (Tweak tweak in toApply)
            {
                if (RowFor(tweak).StageApply()) queued++;
                else AddLog($"Preset {preset.Name}: {tweak.Name} already queued");
            }
            AddLog($"Preset {preset.Name}: queued {queued} " + (queued == 1 ? "tweak" : "tweaks"));
            return;
        }

        var requests = toApply.Select(t => new TweakConfirmRequest(
            t.Name, t.Category, t.Risk, t.RequiresRestart,
            t.Description, t.Benefit, t.Downsides, t.Warning)).ToList();

        if (!_confirm.ConfirmBatch($"Apply {preset.Name}? ({requests.Count} tweaks)", requests))
            return;

        foreach (Tweak tweak in toApply)
        {
            try { _engine.Apply(tweak); AddLog($"Preset {preset.Name}: applied {tweak.Name}"); }
            catch (Exception ex) { AddLog($"ERROR applying {tweak.Name}: {ex.Message}"); }
        }
        RefreshStates();
    }
}
