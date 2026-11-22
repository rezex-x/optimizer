using System.Collections.ObjectModel;
using System.ComponentModel;
using Tweaker.Core.Models;
using Tweaker.Core.Services;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class ConfigViewModel : ObservableObject
{
    private readonly IReadOnlyList<Tweak> _tweaks;
    private readonly ITweakEngine _engine;
    private readonly IConfirmationService _confirm;
    private readonly PendingChangesViewModel? _pending;
    private readonly ActivityLog? _activity;
    private string _searchText = "";
    private string _selectedCategory = MainViewModel.AllCategory;
    private readonly Dictionary<string, TweakViewModel> _rows = new(StringComparer.Ordinal);

    public ConfigViewModel(IReadOnlyList<Tweak> configTweaks, ITweakEngine engine, IConfirmationService confirm,
        PendingChangesViewModel? pending = null, ActivityLog? activity = null)
    {
        _tweaks = configTweaks;
        _engine = engine;
        _confirm = confirm;
        _pending = pending;
        _activity = activity;
        Log = activity?.Lines ?? new ObservableCollection<string>();
        foreach (Tweak t in _tweaks) RowFor(t);
        Categories = new ObservableCollection<string>(
            new[] { MainViewModel.AllCategory }.Concat(_tweaks.Select(t => t.Category).Distinct(StringComparer.Ordinal)));
        Rebuild();
    }

    public ObservableCollection<string> Categories { get; }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (SetField(ref _selectedCategory, value ?? MainViewModel.AllCategory)) Rebuild(); }
    }

    public ObservableCollection<CategoryGroup> Groups { get; } = new();
    public ObservableCollection<CategoryGroup> LeftColumn { get; } = new();
    public ObservableCollection<CategoryGroup> RightColumn { get; } = new();

    public ObservableCollection<string> Log { get; }

    public string SearchText
    {
        get => _searchText;
        set { if (SetField(ref _searchText, value)) Rebuild(); }
    }

    public int AppliedCount => _rows.Values.Count(r => r.IsApplied);

    private void Rebuild()
    {
        Groups.Clear();
        LeftColumn.Clear();
        RightColumn.Clear();

        IEnumerable<Tweak> shown = _tweaks;
        if (_selectedCategory != MainViewModel.AllCategory)
            shown = shown.Where(t => t.Category == _selectedCategory);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string term = SearchText.Trim();
            shown = shown.Where(t =>
                t.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var group in shown.GroupBy(t => t.Category))
        {
            Groups.Add(new CategoryGroup(group.Key, group.Select(RowFor).ToList()));
        }

        int leftHeight = 0, rightHeight = 0;
        foreach (CategoryGroup g in Groups.OrderByDescending(g => g.Tweaks.Count))
        {
            if (leftHeight <= rightHeight) { LeftColumn.Add(g); leftHeight += g.Tweaks.Count; }
            else { RightColumn.Add(g); rightHeight += g.Tweaks.Count; }
        }

        OnPropertyChanged(nameof(AppliedCount));
    }

    private TweakViewModel RowFor(Tweak tweak)
    {
        if (!_rows.TryGetValue(tweak.Id, out TweakViewModel? row))
        {
            row = new TweakViewModel(tweak, _engine, _confirm, AddLog, pending: _pending);
            row.PropertyChanged += OnRowChanged;
            _rows[tweak.Id] = row;
        }
        return row;
    }

    public void RefreshStates()
    {
        foreach (TweakViewModel row in _rows.Values) row.RefreshState();
        OnPropertyChanged(nameof(AppliedCount));
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TweakViewModel.IsApplied))
            OnPropertyChanged(nameof(AppliedCount));
    }

    private void AddLog(string message)
    {
        if (_activity is not null) _activity.Add("Config", message);
        else Log.Add($"{DateTime.Now:HH:mm:ss}  {message}");
    }
}
