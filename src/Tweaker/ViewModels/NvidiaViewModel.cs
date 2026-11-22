using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Tweaker.Core.Nvidia;
using Tweaker.Core.Services;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class NvidiaViewModel : ObservableObject
{
    private readonly INvidiaSettingsService _service;
    private readonly NvidiaBackupStore _backup;
    private readonly IConfirmationService _confirm;
    private readonly IReadOnlyList<NvidiaSetting> _catalog;
    private readonly PendingChangesViewModel? _pending;
    private readonly ActivityLog? _activity;
    private string _selectedCategory = MainViewModel.AllCategory;

    public NvidiaViewModel(INvidiaSettingsService service, NvidiaBackupStore backup,
        IConfirmationService confirm, IReadOnlyList<NvidiaSetting> catalog,
        PendingChangesViewModel? pending = null, ActivityLog? activity = null)
    {
        _service = service; _backup = backup; _confirm = confirm; _catalog = catalog;
        _pending = pending; _activity = activity;
        Log = activity?.Lines ?? new ObservableCollection<string>();
        Settings = new ObservableCollection<NvidiaSettingViewModel>(
            catalog.Select(s => new NvidiaSettingViewModel(s, service, backup, confirm, AddLog, pending)));
        ApplyRecommendedCommand = new RelayCommand(_ => ApplyRecommended());
        Categories = new ObservableCollection<string>(
            new[] { MainViewModel.AllCategory }.Concat(Settings.Select(s => s.Category).Distinct(StringComparer.Ordinal)));
        RebuildGroups();
    }

    public ObservableCollection<string> Categories { get; }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (SetField(ref _selectedCategory, value ?? MainViewModel.AllCategory)) RebuildGroups(); }
    }

    public ObservableCollection<NvidiaGroup> Groups { get; } = new();

    public bool IsAvailable => _service.IsAvailable;
    public string GpuStatus => _service.IsAvailable
        ? (_service.GpuName ?? "NVIDIA GPU detected")
        : "No NVIDIA GPU detected — these settings apply only to NVIDIA cards.";

    public ObservableCollection<NvidiaSettingViewModel> Settings { get; }

    public ObservableCollection<string> Log { get; }

    public ICommand ApplyRecommendedCommand { get; }

    private void RebuildGroups()
    {
        Groups.Clear();
        foreach (IGrouping<string, NvidiaSettingViewModel> group in Settings.GroupBy(s => s.Category, StringComparer.Ordinal))
        {
            if (_selectedCategory == MainViewModel.AllCategory || group.Key == _selectedCategory)
                Groups.Add(new NvidiaGroup(group.Key, group.ToList()));
        }
    }

    private void AddLog(string message)
    {
        if (_activity is not null) _activity.Add("NVIDIA", message);
        else Log.Add($"{System.DateTime.Now:HH:mm:ss}  {message}");
    }

    public void Refresh()
    {
        if (!_service.IsAvailable) return;
        var map = _service.ReadSettings(_catalog.Select(s => s.SettingId).ToList());
        for (int i = 0; i < Settings.Count; i++)
            Settings[i].SetCurrent(map.TryGetValue(_catalog[i].SettingId, out uint? v) ? v : null);
    }

    private void ApplyRecommended()
    {
        if (!_service.IsAvailable) return;
        if (_pending is not null)
        {
            QueueRecommended();
            return;
        }

        var requests = _catalog.Select(s => new TweakConfirmRequest(
            s.Name, "NVIDIA", s.Risk, false, s.Description, s.Benefit, s.Downsides, s.Warning)).ToList();
        if (!_confirm.ConfirmBatch($"Apply recommended NVIDIA settings? ({requests.Count})", requests)) return;

        for (int i = 0; i < _catalog.Count; i++)
        {
            NvidiaSetting s = _catalog[i];
            if (Settings[i].IsApplied) { AddLog($"Already applied: {s.Name}"); continue; }
            try
            {
                uint? prior = _service.ApplySetting(s.SettingId, s.RecommendedValue);
                _backup.StorePriorIfAbsent(s.SettingId, prior);
                Settings[i].SetCurrent(s.RecommendedValue);
                AddLog($"Applied: {s.Name}");
            }
            catch (NvidiaSettingsException ex) { AddLog($"ERROR applying {s.Name}: {ex.Message}"); }
        }
    }

    private void QueueRecommended()
    {
        int queued = 0;
        foreach (NvidiaSettingViewModel setting in Settings)
            if (setting.StageApply()) queued++;

        if (queued > 0)
            AddLog(queued == 1 ? "Queued 1 recommended setting" : $"Queued {queued} recommended settings");
        else if (Settings.All(s => s.IsApplied))
            AddLog("All recommended settings are already applied");
        else
            AddLog("The recommended settings are already queued");
    }
}
