using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class ProfileItemViewModel : ObservableObject
{
    private AppProfile _profile;
    private bool _isSelected, _isActive;

    public ProfileItemViewModel(AppProfile profile) => _profile = profile;

    public string Id => _profile.Id;
    public AppProfile Profile => _profile;
    public string Name => _profile.Name;
    public bool ShowAuto => _profile.AutoActivate && _profile.HasTrigger && _profile.Mode == ProfileMode.CloseList;
    public bool ShowCustom => !_profile.IsBuiltIn;
    public string ModeText => _profile.Mode == ProfileMode.AllowList ? "ALLOWLIST" : "CLOSE LIST";

    public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }

    public void Update(AppProfile profile)
    {
        _profile = profile;
        OnPropertyChanged(nameof(Profile));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ShowAuto));
        OnPropertyChanged(nameof(ShowCustom));
        OnPropertyChanged(nameof(ModeText));
    }
}

public sealed class ProfileEntryViewModel
{
    public ProfileEntryViewModel(string entry, string text, string detail)
    {
        Entry = entry;
        Text = text;
        Detail = detail;
    }

    public string Entry { get; }
    public string Text { get; }
    public string Detail { get; }
    public bool HasDetail => Detail.Length > 0;
}

public sealed class ProfilesViewModel : ObservableObject
{
    private static readonly TimeSpan ConfirmDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromSeconds(60);
    private const string NeverTouched = "Windows, drivers, security software and anti-cheat processes are never touched.";

    private readonly ProfileManager _manager;
    private readonly Func<DateTime> _now;
    private IReadOnlyDictionary<string, string> _serviceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private ProfileItemViewModel? _selected;
    private ProfilePlan? _plan;
    private string _previewId = "";
    private DateTime _previewAt, _deleteArmedAt;
    private bool _isPreviewing, _isDeleteArmed, _isWorking;
    private string _message = "", _newApp = "", _newService = "";
    private string _previewTitle = "", _previewCloseText = "", _previewStopText = "";

    public ProfilesViewModel(ProfileManager manager, Func<DateTime>? now = null)
    {
        _manager = manager;
        _now = now ?? (() => DateTime.UtcNow);
        foreach (AppProfile p in manager.Profiles) Items.Add(new ProfileItemViewModel(p));

        SetModeCommand = new RelayCommand(p => { if (Enum.TryParse(p?.ToString(), out ProfileMode m)) Edit(x => x with { Mode = m, AutoActivate = m == ProfileMode.CloseList && x.AutoActivate }); });
        AddAppCommand = new RelayCommand(_ => AddApp());
        AddServiceCommand = new RelayCommand(_ => AddService());
        RemoveAppCommand = new RelayCommand(p => { if (p is ProfileEntryViewModel e) RemoveApp(e); });
        RemoveServiceCommand = new RelayCommand(p => { if (p is ProfileEntryViewModel e) RemoveService(e); });
        NewCommand = new RelayCommand(_ => CreateProfile());
        DeleteCommand = new RelayCommand(_ => ArmDelete());
        ConfirmDeleteCommand = new RelayCommand(_ => ConfirmDelete());
        ActivateCommand = new RelayCommand(async _ => await ActivateAsync());
        ConfirmActivateCommand = new RelayCommand(async _ => await ConfirmActivateAsync());
        CancelPreviewCommand = new RelayCommand(_ => CancelPreview());
        DeactivateCommand = new RelayCommand(async _ => await DeactivateAsync());

        manager.PropertyChanged += OnManagerChanged;
        Select(Items.FirstOrDefault(i => i.Id == manager.ActiveId) ?? Items.FirstOrDefault());
        RefreshActive();
    }

    public ProfileManager Manager => _manager;
    public ObservableCollection<ProfileItemViewModel> Items { get; } = new();
    public ObservableCollection<ProfileEntryViewModel> Apps { get; } = new();
    public ObservableCollection<ProfileEntryViewModel> Services { get; } = new();

    public ProfileItemViewModel? Selected
    {
        get => _selected;
        set => Select(value);
    }

    private AppProfile? Current => _selected?.Profile;
    public bool HasSelection => _selected is not null;

    public string Name
    {
        get => Current?.Name ?? "";
        set
        {
            string name = (value ?? "").Trim();
            if (name.Length > 0) Edit(p => p with { Name = name });
            else OnPropertyChanged();
        }
    }

    public string Description
    {
        get => Current?.Description ?? "";
        set => Edit(p => p with { Description = (value ?? "").Trim() });
    }

    public string Trigger
    {
        get => Current?.Trigger ?? "";
        set
        {
            string exe = ProfileRules.NormalizeExe(value);
            Edit(p => p with { Trigger = exe, AutoActivate = exe.Length > 0 && p.AutoActivate });
        }
    }

    public bool AutoActivate
    {
        get => Current is { } p && p.AutoActivate && p.HasTrigger && p.Mode == ProfileMode.CloseList;
        set => Edit(p => p with { AutoActivate = value && p.HasTrigger && p.Mode == ProfileMode.CloseList });
    }

    public bool RestoreServices
    {
        get => Current?.RestoreServices ?? true;
        set => Edit(p => p with { RestoreServices = value });
    }

    public bool CanAutoActivate => Current is { } p && p.HasTrigger && p.Mode == ProfileMode.CloseList;
    public bool IsAllowList => Current?.Mode == ProfileMode.AllowList;
    public bool IsCloseList => !IsAllowList;
    public ICommand SetModeCommand { get; }

    public string AllowNote => Current is { } p && p.HasTrigger
        ? "Every other app you started is closed while " + p.Name + " runs. " + NeverTouched
        : "Every other app you started is closed while this profile is active. " + NeverTouched;

    public string AppsTitle => (IsAllowList ? "Allowed apps" : "Apps to close") + " (" + Apps.Count.ToString(CultureInfo.InvariantCulture) + ")";
    public string ServicesTitle => "Services to stop (" + Services.Count.ToString(CultureInfo.InvariantCulture) + ")";

    public string NewApp { get => _newApp; set => SetField(ref _newApp, value ?? ""); }
    public string NewService { get => _newService; set => SetField(ref _newService, value ?? ""); }
    public ICommand AddAppCommand { get; }
    public ICommand AddServiceCommand { get; }
    public ICommand RemoveAppCommand { get; }
    public ICommand RemoveServiceCommand { get; }

    public bool IsCustom => Current is { IsBuiltIn: false };
    public bool ShowDelete => IsCustom && !_isDeleteArmed;
    public bool ShowConfirmDelete => IsCustom && _isDeleteArmed;
    public ICommand NewCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }

    public bool IsSelectedActive => Current is not null && _manager.ActiveId == Current.Id;
    public bool ShowActivate => !IsSelectedActive && !_isPreviewing;
    public bool CanActivate => !_manager.IsActive && !_manager.IsBusy && !_isWorking;
    public bool IsOrphanActive => _manager.IsActive && _manager.Find(_manager.ActiveId) is null;
    public bool ShowDeactivate => IsSelectedActive || IsOrphanActive;
    public bool IsPreviewing => _isPreviewing;
    public bool IsPreviewEmpty => _plan is not null && _plan.IsEmpty;
    public bool HasPreviewItems => _plan is not null && !_plan.IsEmpty;
    public string PreviewTitle => _previewTitle;
    public string PreviewCloseText => _previewCloseText;
    public string PreviewStopText => _previewStopText;
    public ICommand ActivateCommand { get; }
    public ICommand ConfirmActivateCommand { get; }
    public ICommand CancelPreviewCommand { get; }
    public ICommand DeactivateCommand { get; }

    public string Message { get => _message; private set => SetField(ref _message, value); }

    public async Task RefreshAsync()
    {
        _serviceNames = await _manager.ServiceNamesAsync();
        LoadEntries();
    }

    public async Task ActivateAsync()
    {
        AppProfile? profile = Current;
        if (profile is null || _isWorking || _isPreviewing) return;
        if (_manager.Session is { } active)
        {
            Message = active.ProfileName + " is already active — deactivate it first.";
            return;
        }

        SetWorking(true);
        ProfilePlan plan;
        try { plan = await _manager.PreviewAsync(profile); }
        finally { SetWorking(false); }
        if (_selected?.Id != profile.Id) return;

        _plan = plan;
        _previewId = profile.Id;
        _previewAt = _now();
        _previewTitle = "Activating " + profile.Name + " will:";
        _previewCloseText = CloseText(plan);
        _previewStopText = plan.Stop.Count == 0 ? "nothing" : string.Join(", ", plan.Stop.Select(s => s.DisplayName));
        _isPreviewing = true;
        RaisePreview();
    }

    public async Task ConfirmActivateAsync()
    {
        if (!_isPreviewing || _plan is null || _isWorking || _now() - _previewAt < ConfirmDelay) return;
        if (_now() - _previewAt > PreviewLifetime)
        {
            CancelPreview();
            Message = "Preview expired — click Activate now again.";
            return;
        }
        ProfilePlan plan = _plan;
        AppProfile? profile = _manager.Find(_previewId);
        CancelPreview();
        if (profile is null) return;

        SetWorking(true);
        Message = "Activating " + profile.Name + "…";
        try { Message = await _manager.ActivateAsync(profile, plan, automatic: false); }
        finally { SetWorking(false); }
    }

    public async Task DeactivateAsync()
    {
        if (_isWorking) return;
        SetWorking(true);
        Message = "Deactivating…";
        try { Message = await _manager.DeactivateByHandAsync(); }
        finally { SetWorking(false); }
    }

    private void CancelPreview()
    {
        if (!_isPreviewing && _plan is null) return;
        _isPreviewing = false;
        _plan = null;
        RaisePreview();
    }

    private static string CloseText(ProfilePlan plan)
    {
        if (plan.Close.Count == 0) return "nothing";
        return string.Join(", ", plan.Close
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Count() == 1 ? g.Key : g.Key + " ×" + g.Count().ToString(CultureInfo.InvariantCulture)));
    }

    private void Select(ProfileItemViewModel? item)
    {
        if (ReferenceEquals(item, _selected)) return;
        if (_selected is not null) _selected.IsSelected = false;
        _selected = item;
        if (item is not null) item.IsSelected = true;
        _isDeleteArmed = false;
        NewApp = "";
        NewService = "";
        CancelPreview();
        OnPropertyChanged(nameof(Selected));
        LoadEntries();
        RaiseEditor();
    }

    private void Edit(Func<AppProfile, AppProfile> change)
    {
        if (_selected is null) return;
        AppProfile updated = change(_selected.Profile);
        if (updated == _selected.Profile)
        {
            RaiseEditor();
            return;
        }
        CancelPreview();
        _manager.Save(updated);
        _selected.Update(updated);
        RaiseEditor();
    }

    private void AddApp()
    {
        string exe = ProfileRules.NormalizeExe(NewApp);
        NewApp = "";
        if (exe.Length == 0 || Current is null || Current.Apps.Any(a => ProfileRules.SameExe(a, exe))) return;
        Edit(p => p with { Apps = p.Apps.Append(exe).ToList() });
        LoadEntries();
    }

    private void RemoveApp(ProfileEntryViewModel entry)
    {
        Edit(p => p with { Apps = p.Apps.Where(a => a != entry.Entry).ToList() });
        LoadEntries();
    }

    private void AddService()
    {
        string typed = NewService.Trim();
        NewService = "";
        if (typed.Length == 0 || Current is null) return;
        string name = ServiceNameOf(typed) ?? typed;
        bool listed = Current.Services.Any(e =>
            string.Equals(ServiceNameOf(e) ?? e.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (listed) return;
        Edit(p => p with { Services = p.Services.Append(name).ToList() });
        LoadEntries();
    }

    private void RemoveService(ProfileEntryViewModel entry)
    {
        Edit(p => p with { Services = p.Services.Where(s => s != entry.Entry).ToList() });
        LoadEntries();
    }

    private string? ServiceNameOf(string entry)
    {
        string e = entry.Trim();
        if (_serviceNames.ContainsKey(e)) return _serviceNames.Keys.First(k => string.Equals(k, e, StringComparison.OrdinalIgnoreCase));
        foreach (KeyValuePair<string, string> kv in _serviceNames)
        {
            if (string.Equals(kv.Value, e, StringComparison.OrdinalIgnoreCase)) return kv.Key;
        }
        return null;
    }

    private void LoadEntries()
    {
        Apps.Clear();
        Services.Clear();
        if (Current is not null)
        {
            foreach (string app in Current.Apps) Apps.Add(new ProfileEntryViewModel(app, app, ""));
            foreach (string entry in Current.Services)
            {
                string? name = ServiceNameOf(entry);
                Services.Add(name is null
                    ? new ProfileEntryViewModel(entry, entry, "")
                    : new ProfileEntryViewModel(entry, _serviceNames[name], name));
            }
        }
        OnPropertyChanged(nameof(AppsTitle));
        OnPropertyChanged(nameof(ServicesTitle));
    }

    private void CreateProfile()
    {
        AppProfile profile = _manager.Create();
        var item = new ProfileItemViewModel(profile);
        Items.Add(item);
        Select(item);
        Message = "Created " + profile.Name;
    }

    private void ArmDelete()
    {
        if (Current is not { IsBuiltIn: false } profile) return;
        if (IsSelectedActive)
        {
            Message = "Deactivate " + profile.Name + " before deleting it.";
            return;
        }
        _isDeleteArmed = true;
        _deleteArmedAt = _now();
        Message = "Delete " + profile.Name + "? Click Confirm to delete it.";
        RaiseEditor();
    }

    private void ConfirmDelete()
    {
        if (!_isDeleteArmed || _selected is null || _now() - _deleteArmedAt < ConfirmDelay) return;
        ProfileItemViewModel item = _selected;
        string name = item.Name;
        _isDeleteArmed = false;
        if (!_manager.Delete(item.Id))
        {
            Message = "Couldn't delete " + name + ".";
            RaiseEditor();
            return;
        }
        Items.Remove(item);
        _selected = null;
        Select(Items.FirstOrDefault());
        Message = "Deleted " + name;
    }

    private void OnManagerChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ProfileManager.LastMessage):
                Message = _manager.LastMessage;
                break;
            case nameof(ProfileManager.IsActive):
            case nameof(ProfileManager.ActiveId):
            case nameof(ProfileManager.IsBusy):
                RefreshActive();
                break;
        }
    }

    private void RefreshActive()
    {
        foreach (ProfileItemViewModel item in Items) item.IsActive = item.Id == _manager.ActiveId;
        OnPropertyChanged(nameof(IsSelectedActive));
        OnPropertyChanged(nameof(ShowActivate));
        OnPropertyChanged(nameof(CanActivate));
        OnPropertyChanged(nameof(IsOrphanActive));
        OnPropertyChanged(nameof(ShowDeactivate));
        if (IsOrphanActive && !_manager.IsBusy && !_isWorking && _manager.Session is { } orphan)
            Message = orphan.ProfileName + " is still active but its profile no longer exists — Deactivate restores its services.";
    }

    private void SetWorking(bool working)
    {
        _isWorking = working;
        OnPropertyChanged(nameof(CanActivate));
    }

    private void RaisePreview()
    {
        OnPropertyChanged(nameof(IsPreviewing));
        OnPropertyChanged(nameof(IsPreviewEmpty));
        OnPropertyChanged(nameof(HasPreviewItems));
        OnPropertyChanged(nameof(PreviewTitle));
        OnPropertyChanged(nameof(PreviewCloseText));
        OnPropertyChanged(nameof(PreviewStopText));
        OnPropertyChanged(nameof(ShowActivate));
    }

    private void RaiseEditor()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Trigger));
        OnPropertyChanged(nameof(AutoActivate));
        OnPropertyChanged(nameof(RestoreServices));
        OnPropertyChanged(nameof(CanAutoActivate));
        OnPropertyChanged(nameof(IsAllowList));
        OnPropertyChanged(nameof(IsCloseList));
        OnPropertyChanged(nameof(AllowNote));
        OnPropertyChanged(nameof(AppsTitle));
        OnPropertyChanged(nameof(ServicesTitle));
        OnPropertyChanged(nameof(IsCustom));
        OnPropertyChanged(nameof(ShowDelete));
        OnPropertyChanged(nameof(ShowConfirmDelete));
        OnPropertyChanged(nameof(IsSelectedActive));
        OnPropertyChanged(nameof(ShowActivate));
        OnPropertyChanged(nameof(CanActivate));
        OnPropertyChanged(nameof(IsOrphanActive));
        OnPropertyChanged(nameof(ShowDeactivate));
    }
}
