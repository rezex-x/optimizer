using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Tweaker.Core.Catalog;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class RemovableAppRowViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    private bool _isInstalled, _isSelected;

    public RemovableAppRowViewModel(RemovableApp app, Action selectionChanged)
    {
        App = app;
        _selectionChanged = selectionChanged;
    }

    public RemovableApp App { get; }
    public string Name => App.Name;
    public string PackageName => App.PackageName;
    public Impact Impact => App.Impact;
    public bool Recommended => App.Recommended;
    public string Note => App.Note;
    public bool HasNote => !string.IsNullOrWhiteSpace(App.Note);

    public bool IsInstalled
    {
        get => _isInstalled;
        internal set
        {
            if (!SetField(ref _isInstalled, value)) return;
            OnPropertyChanged(nameof(CanSelect));
            if (!value) IsSelected = false;
        }
    }

    public bool CanSelect => _isInstalled;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetField(ref _isSelected, value && _isInstalled)) _selectionChanged();
        }
    }
}

public sealed class AppRemovalViewModel : ObservableObject
{
    public const string ToolName = "Remove Preinstalled Apps";

    private readonly IAppxService _appx;
    private readonly Action<string> _log;
    private Task? _loading;
    private bool _loadedOnce, _isExpanded, _isLoading;
    private string? _loadError;
    private int _bulk;

    public AppRemovalViewModel(IAppxService appx, Action<string> log)
    {
        _appx = appx;
        _log = log;
        foreach (RemovableApp app in RemovableApps.All.OrderBy(a => a.Impact).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            Rows.Add(new RemovableAppRowViewModel(app, OnRowSelectionChanged));
        SelectRecommendedCommand = new RelayCommand(_ => SelectRecommended());
        SelectAllCommand = new RelayCommand(_ => Bulk(() => { foreach (var r in Rows) r.IsSelected = r.IsInstalled; }));
        SelectNoneCommand = new RelayCommand(_ => Bulk(() => { foreach (var r in Rows) r.IsSelected = false; }));
        ToggleExpandedCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
    }

    public event EventHandler? SelectionChanged;

    public ObservableCollection<RemovableAppRowViewModel> Rows { get; } = new();

    public ICommand SelectRecommendedCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand SelectNoneCommand { get; }
    public ICommand ToggleExpandedCommand { get; }

    public bool IsExpanded { get => _isExpanded; set => SetField(ref _isExpanded, value); }

    public bool IsLoading
    {
        get => _isLoading;
        private set { if (SetField(ref _isLoading, value)) OnPropertyChanged(nameof(Summary)); }
    }

    public string? LoadError
    {
        get => _loadError;
        private set { if (SetField(ref _loadError, value)) OnPropertyChanged(nameof(HasLoadError)); }
    }

    public bool HasLoadError => !string.IsNullOrEmpty(_loadError);

    public IReadOnlyList<RemovableAppRowViewModel> Selected => Rows.Where(r => r.IsInstalled && r.IsSelected).ToList();

    public int SelectedCount => Rows.Count(r => r.IsInstalled && r.IsSelected);

    public string Summary
    {
        get
        {
            if (_isLoading) return "Checking installed apps…";
            int installed = Rows.Count(r => r.IsInstalled);
            if (installed == 0) return "No removable apps found";
            return SelectedCount.ToString(CultureInfo.InvariantCulture) + " of " +
                   installed.ToString(CultureInfo.InvariantCulture) + " installed apps selected";
        }
    }

    public Task LoadAsync()
    {
        Task previous = _loading ?? Task.CompletedTask;
        _loading = LoadAfterAsync(previous);
        return _loading;
    }

    public int LastFailedCount { get; private set; }

    public async Task<int> RemoveSelectedAsync()
    {
        LastFailedCount = 0;
        List<RemovableAppRowViewModel> selected = Selected.ToList();
        if (selected.Count == 0)
        {
            _log(ToolName + ": no apps selected");
            return 0;
        }
        _log("Running " + ToolName);
        List<string> packages = selected.Select(r => r.PackageName).ToList();
        IReadOnlyList<AppxRemovalResult> results = await Task.Run<IReadOnlyList<AppxRemovalResult>>(() =>
        {
            try { return _appx.RemoveMany(packages); }
            catch (Exception ex) { return packages.Select(p => new AppxRemovalResult(p, AppxRemoval.Failed, ex.Message)).ToList(); }
        });
        var byPackage = new Dictionary<string, AppxRemovalResult>(StringComparer.OrdinalIgnoreCase);
        foreach (AppxRemovalResult r in results) byPackage[r.PackageName] = r;

        int removed = 0, failed = 0;
        foreach (RemovableAppRowViewModel row in selected)
        {
            AppxRemovalResult result = byPackage.TryGetValue(row.PackageName, out AppxRemovalResult? r)
                ? r : new AppxRemovalResult(row.PackageName, AppxRemoval.Failed, "no result");
            switch (result.Result)
            {
                case AppxRemoval.Removed:
                    removed++;
                    _log("    " + row.Name + " — removed");
                    break;
                case AppxRemoval.NotInstalled:
                    _log("    " + row.Name + " — not installed");
                    break;
                default:
                    failed++;
                    _log("    " + row.Name + " — failed: " + result.Reason);
                    break;
            }
        }
        LastFailedCount = failed;
        _log("Removed " + removed.ToString(CultureInfo.InvariantCulture) + " of " +
             selected.Count.ToString(CultureInfo.InvariantCulture) + (selected.Count == 1 ? " app" : " apps"));
        await LoadAsync();
        return removed;
    }

    private async Task LoadAfterAsync(Task previous)
    {
        try { await previous; }
        catch { }
        await LoadCoreAsync();
    }

    private async Task LoadCoreAsync()
    {
        IsLoading = true;
        try
        {
            IReadOnlySet<string>? names = await Task.Run(() =>
            {
                try { return _appx.InstalledPackageNames(); }
                catch { return null; }
            });
            Bulk(() =>
            {
                if (names is null)
                {
                    LoadError = "Couldn't list the installed apps.";
                    foreach (RemovableAppRowViewModel row in Rows) row.IsInstalled = false;
                    return;
                }
                LoadError = null;
                var installed = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
                foreach (RemovableAppRowViewModel row in Rows) row.IsInstalled = installed.Contains(row.PackageName);
                if (!_loadedOnce)
                {
                    _loadedOnce = true;
                    foreach (RemovableAppRowViewModel row in Rows) row.IsSelected = row.Recommended;
                }
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SelectRecommended() => Bulk(() => { foreach (var r in Rows) r.IsSelected = r.IsInstalled && r.Recommended; });

    private void Bulk(Action change)
    {
        _bulk++;
        try { change(); }
        finally { _bulk--; }
        RaiseSelection();
    }

    private void OnRowSelectionChanged()
    {
        if (_bulk == 0) RaiseSelection();
    }

    private void RaiseSelection()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(Summary));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
