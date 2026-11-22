using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using Tweaker.Core.Catalog;
using Tweaker.Core.Services;
using Tweaker.Resources;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class AppRow : ObservableObject
{
    private bool _selected, _isInstalled;
    private string _installedTip = "";
    public AppRow(InstallApp app, Action<AppRow> onToggle)
    {
        App = app;
        ToggleCommand = new RelayCommand(_ => { if (IsInstalled) return; Selected = !Selected; onToggle(this); });
    }
    public InstallApp App { get; }
    public string WingetId => App.WingetId;
    public string Name => App.Name;
    public string Description => App.Description;
    public string Category => App.Category;

    public bool Selected
    {
        get => _selected;
        set { if (SetField(ref _selected, value)) OnPropertyChanged(nameof(IsTicked)); }
    }

    public bool IsInstalled => _isInstalled;

    public bool IsTicked => _selected || _isInstalled;

    public string InstalledTip => _installedTip;

    internal void SetInstalled(bool installed, string tip)
    {
        if (SetField(ref _isInstalled, installed, nameof(IsInstalled))) OnPropertyChanged(nameof(IsTicked));
        SetField(ref _installedTip, installed ? tip : "", nameof(InstalledTip));
    }
    public ICommand ToggleCommand { get; }

    public string Initial => string.IsNullOrWhiteSpace(App.Name) ? "?" : App.Name.TrimStart().Substring(0, 1).ToUpperInvariant();

    public ImageSource? IconImage => AppIconImages.Get(App.WingetId);

    public bool HasImageIcon => AppIconImages.Has(App.WingetId);

    public bool HasBrandIcon => !HasImageIcon && BrandIcons.TryGetPath(App.WingetId, out _);

    public bool HasMonogram => !HasImageIcon && !HasBrandIcon;

    public Geometry? BrandGeometry =>
        BrandIcons.TryGetPath(App.WingetId, out string d) ? Geometry.Parse(d) : null;

    public Brush BrandBrush =>
        BrandIcons.TryGetColor(App.WingetId, out string hex)
            ? new SolidColorBrush(Readable((Color)ColorConverter.ConvertFromString(hex)))
            : new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));

    public Brush TileBrush => HasBrandIcon || HasImageIcon ? new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)) : new SolidColorBrush(CategoryColor(App.Category));

    public Brush MonogramBrush => new SolidColorBrush(CategoryColor(App.Category));

    private static Color Readable(Color c)
    {
        double lum = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        if (lum >= 90) return c;
        double t = System.Math.Min(0.78, (90 - lum) / 255.0 + 0.4);
        byte Lift(byte v) => (byte)(v + (255 - v) * t);
        return Color.FromRgb(Lift(c.R), Lift(c.G), Lift(c.B));
    }

    private static Color CategoryColor(string category) => category switch
    {
        "Browsers"    => Color.FromRgb(0x24, 0x3A, 0x2A),
        "Development" => Color.FromRgb(0x24, 0x30, 0x3A),
        "Utilities"   => Color.FromRgb(0x33, 0x2E, 0x22),
        "Media"       => Color.FromRgb(0x33, 0x24, 0x30),
        "Gaming"      => Color.FromRgb(0x2A, 0x24, 0x3A),
        "Security"    => Color.FromRgb(0x3A, 0x24, 0x24),
        "System tools" => Color.FromRgb(0x22, 0x33, 0x33),
        _             => Color.FromRgb(0x22, 0x22, 0x22),
    };
}

public sealed class InstallViewModel : ObservableObject
{
    public const string AllCategory = "All";
    private readonly IWingetService _winget;
    private readonly ActivityLog? _activity;
    private readonly HashSet<string> _selected = new();
    private string _selectedCategory = AllCategory;
    private string _searchText = "";
    private int _selectedCount;
    private bool _isRunning, _isCheckingInstalled;
    private readonly IInstalledAppDetector? _detector;
    private readonly IInstalledStore? _store;
    private readonly Func<DateTime> _now;
    private readonly Dictionary<string, string> _installed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _found = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _session = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _stored = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _recordedThisSession = new(StringComparer.OrdinalIgnoreCase);
    private Task? _detectTask;

    public InstallViewModel(IWingetService winget, ActivityLog? activity = null, IInstalledAppDetector? detector = null,
        IInstalledStore? store = null, Func<DateTime>? now = null)
    {
        _winget = winget;
        _activity = activity;
        _detector = detector;
        _store = store;
        _now = now ?? (() => DateTime.Now);
        Log = activity?.Lines ?? new ObservableCollection<string>();
        WingetAvailable = winget.IsAvailable();

        Categories = new ObservableCollection<string> { AllCategory };
        foreach (var c in AppCatalog.Apps.Select(a => a.Category).Distinct())
            Categories.Add(c);

        if (activity is null) System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Log, new object());

        ToggleAppCommand = new RelayCommand(p => ToggleById(p?.ToString()));
        ClearCommand = new RelayCommand(_ => ClearSelection());
        InstallSelectedCommand = new RelayCommand(_ => StartInstall(), _ => WingetAvailable && SelectedCount > 0 && !IsRunning);

        Rebuild();
    }

    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<AppRow> Apps { get; } = new();

    public ObservableCollection<string> Log { get; }

    public bool WingetAvailable { get; }

    public ICommand ToggleAppCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand InstallSelectedCommand { get; }

    public string SelectedCategory { get => _selectedCategory; set { if (SetField(ref _selectedCategory, value)) Rebuild(); } }
    public string SearchText { get => _searchText; set { if (SetField(ref _searchText, value)) Rebuild(); } }
    public int SelectedCount { get => _selectedCount; private set => SetField(ref _selectedCount, value); }
    public bool IsRunning { get => _isRunning; private set => SetField(ref _isRunning, value); }

    public bool IsCheckingInstalled { get => _isCheckingInstalled; private set => SetField(ref _isCheckingInstalled, value); }

    public Task EnsureDetectedAsync() => _detectTask ??= DetectCoreAsync();

    public Task DetectInstalledAsync()
    {
        if (_detectTask is { IsCompleted: false }) return _detectTask;
        _detectTask = DetectCoreAsync();
        return _detectTask;
    }

    private async Task DetectCoreAsync()
    {
        IsCheckingInstalled = true;
        try
        {
            IInstalledAppDetector? detector = _detector;
            IInstalledStore? store = _store;
            (IReadOnlySet<string> found, IReadOnlyDictionary<string, DateTime> stored) = await Task.Run(() =>
            {
                IReadOnlyDictionary<string, DateTime> s;
                try { s = store?.Installed ?? new Dictionary<string, DateTime>(); }
                catch { s = new Dictionary<string, DateTime>(); }
                IReadOnlySet<string> f;
                try { f = detector?.Detect(AppCatalog.Apps) ?? new HashSet<string>(); }
                catch { f = new HashSet<string>(); }
                return (f, s);
            });
            _found.Clear();
            _found.UnionWith(found);
            _stored.Clear();
            foreach (KeyValuePair<string, DateTime> kv in stored) _stored[kv.Key] = kv.Value;
            foreach (KeyValuePair<string, DateTime> kv in _recordedThisSession) _stored.TryAdd(kv.Key, kv.Value);
            RecomputeInstalled();
            foreach (AppRow row in Apps) ApplyInstalled(row);
        }
        finally
        {
            IsCheckingInstalled = false;
        }
    }

    private void RecomputeInstalled()
    {
        _installed.Clear();
        foreach (InstallApp app in AppCatalog.Apps)
        {
            string id = app.WingetId;
            if (!_found.Contains(id) && !_session.Contains(id)) continue;
            _installed[id] = _stored.TryGetValue(id, out DateTime when) ? TweakerTip(when) : "Already on this PC";
        }
        DropInstalledFromSelection();
    }

    private static string TweakerTip(DateTime when) =>
        "Installed by Tweaker on " + when.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private void ApplyInstalled(AppRow row)
    {
        row.SetInstalled(_installed.TryGetValue(row.WingetId, out string? tip), tip ?? "");
        if (row.IsInstalled && row.Selected) row.Selected = false;
    }

    private void DropInstalledFromSelection()
    {
        if (_selected.RemoveWhere(id => _installed.ContainsKey(id)) > 0) SelectedCount = _selected.Count;
    }

    private void MarkInstalled(string id, bool byTweaker)
    {
        DateTime when = _now();
        if (byTweaker)
        {
            try { _store?.Record(id, when); }
            catch { }
        }
        OnUi(() =>
        {
            if (byTweaker)
            {
                _recordedThisSession.TryAdd(id, when);
                _stored.TryAdd(id, when);
            }
            _session.Add(id);
            RecomputeInstalled();
            foreach (AppRow row in Apps.Where(r => string.Equals(r.WingetId, id, StringComparison.OrdinalIgnoreCase)))
                ApplyInstalled(row);
        });
    }

    private static void OnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    private void Rebuild()
    {
        Apps.Clear();
        IEnumerable<InstallApp> apps = AppCatalog.Apps;
        if (SelectedCategory != AllCategory)
            apps = apps.Where(a => a.Category == SelectedCategory);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string t = SearchText.Trim();
            apps = apps.Where(a => a.Name.Contains(t, StringComparison.OrdinalIgnoreCase)
                                || a.Description.Contains(t, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var a in apps)
        {
            var row = new AppRow(a, OnRowToggled) { Selected = _selected.Contains(a.WingetId) };
            ApplyInstalled(row);
            Apps.Add(row);
        }
    }

    private void OnRowToggled(AppRow row)
    {
        if (row.Selected) _selected.Add(row.WingetId);
        else _selected.Remove(row.WingetId);
        SelectedCount = _selected.Count;
    }

    private void ToggleById(string? id)
    {
        if (id is null || _installed.ContainsKey(id)) return;
        AppRow? row = Apps.FirstOrDefault(r => r.WingetId == id);
        if (row is not null) { row.Selected = !row.Selected; OnRowToggled(row); }
        else { if (!_selected.Remove(id)) _selected.Add(id); SelectedCount = _selected.Count; }
    }

    private void ClearSelection()
    {
        _selected.Clear();
        SelectedCount = 0;
        foreach (var r in Apps) r.Selected = false;
    }

    public int InstallSelected(Action<string> log)
    {
        int ok = 0;
        foreach (string id in _selected.ToList())
        {
            string name = AppCatalog.Apps.FirstOrDefault(a => a.WingetId == id)?.Name ?? id;
            log($"Installing {name}…");
            InstallResult r = _winget.Install(id);
            log($"{name}: {r.Message}");
            if (!r.Success) continue;
            ok++;
            MarkInstalled(id, byTweaker: r.ExitCode == 0);
        }
        return ok;
    }

    private void AddLog(string message)
    {
        if (_activity is not null) _activity.Add("Install", message);
        else Log.Add($"{DateTime.Now:HH:mm:ss}  {message}");
    }

    private void StartInstall()
    {
        IsRunning = true;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        void Post(Action a) { if (dispatcher is not null) dispatcher.Invoke(a); else a(); }
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                InstallSelected(msg => Post(() => AddLog(msg)));
            }
            catch (Exception ex)
            {
                Post(() => AddLog($"ERROR during install: {ex.Message}"));
            }
            finally
            {
                Post(() => { IsRunning = false; System.Windows.Input.CommandManager.InvalidateRequerySuggested(); });
            }
        });
    }
}
