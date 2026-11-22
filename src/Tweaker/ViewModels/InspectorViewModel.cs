using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class ConfigEntryViewModel : ObservableObject
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private string _text;
    private string _original;

    public ConfigEntryViewModel(int index, ConfigEntry entry, EntryShape shape)
    {
        Index = index;
        Section = entry.Section;
        Key = entry.Key;
        Shape = shape;
        _text = entry.Value;
        _original = entry.Value;
        Options = shape.Options ?? Array.Empty<ChoiceOption>();
    }

    public int Index { get; }
    public string Section { get; }
    public string Key { get; }
    public EntryShape Shape { get; }
    public string Original => _original;
    public bool IsDirty => _text != _original;

    public string Text
    {
        get => _text;
        set
        {
            value = (value ?? "").Replace("\r", "").Replace("\n", "");
            if (SetField(ref _text, value)) RaiseValue();
        }
    }

    public bool IsToggle => Shape.Kind == EditorKind.Toggle;
    public bool IsSlider => Shape.Kind == EditorKind.Slider;
    public bool IsChoice => Shape.Kind == EditorKind.Choice;
    public bool IsText => Shape.Kind == EditorKind.Text;
    public double Min => Shape.Min;
    public double Max => Shape.Max;
    public double Step => Shape.Step;
    public string MinText => Shape.Min.ToString("0.##", Inv);
    public string MaxText => Shape.Max.ToString("0.##", Inv);
    public string Hint => Shape.Hint;
    public bool HasHint => !Shape.Guessed && Shape.Hint.Length > 0;
    public bool IsGuessed => Shape.Guessed;
    public IReadOnlyList<ChoiceOption> Options { get; }

    public bool IsOn
    {
        get => _text.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        set => Text = ValueTyping.FormatBool(value, _original);
    }

    public double Number
    {
        get => double.TryParse(_text.Trim(), NumberStyles.Float, Inv, out double d) ? d : Shape.Min;
        set => Text = ValueTyping.FormatNumber(value, Shape.IsFloat, _original, Shape.Step);
    }

    public string NumberText
    {
        get => _text;
        set
        {
            if (double.TryParse((value ?? "").Trim(), NumberStyles.Float, Inv, out double d) && double.IsFinite(d))
            {
                if (!Shape.Guessed) d = Math.Clamp(d, Shape.Min, Shape.Max);
                Number = d;
            }
            else OnPropertyChanged();
        }
    }

    public ChoiceOption? SelectedOption
    {
        get => Options.FirstOrDefault(o => string.Equals(o.Value, _text.Trim(), StringComparison.OrdinalIgnoreCase));
        set { if (value is not null) Text = value.Value; }
    }

    public void Revert() => Text = _original;

    public void MarkSaved(string savedText)
    {
        _original = savedText;
        OnPropertyChanged(nameof(Original));
        OnPropertyChanged(nameof(IsDirty));
    }

    private void RaiseValue()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsOn));
        OnPropertyChanged(nameof(Number));
        OnPropertyChanged(nameof(NumberText));
        OnPropertyChanged(nameof(SelectedOption));
    }
}

public sealed class ConfigSectionViewModel
{
    public ConfigSectionViewModel(string name, IReadOnlyList<ConfigEntryViewModel> entries)
    {
        Name = name;
        Entries = entries;
        Title = name.Length == 0 ? "(no section)" : "[" + name + "]";
    }

    public string Name { get; }
    public string Title { get; }
    public IReadOnlyList<ConfigEntryViewModel> Entries { get; }
}

public sealed class SectionItem : ObservableObject
{
    private bool _isActive;

    public SectionItem(string? name)
    {
        Name = name;
        Label = name is null ? "All" : name.Length == 0 ? "(no section)" : name;
    }

    public string? Name { get; }
    public string Label { get; }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
}

public sealed class DetectedConfigViewModel
{
    public DetectedConfigViewModel(DetectedConfig config, string displayPath, bool hasPreset)
    {
        Game = config.Game;
        FileName = config.FileName;
        Path = config.Path;
        DisplayPath = displayPath;
        HasPreset = hasPreset;
    }

    public string Game { get; }
    public string FileName { get; }
    public string Path { get; }
    public string DisplayPath { get; }
    public bool HasPreset { get; }
}

public sealed class InspectorViewModel : ObservableObject
{
    private static readonly TimeSpan ConfirmDelay = TimeSpan.FromMilliseconds(500);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly IConfigFileStore _files;
    private readonly IRunningExeSource _running;
    private readonly Func<IReadOnlyList<DetectedConfig>> _detect;
    private readonly Func<string, string> _display;
    private readonly Func<DateTime> _now;
    private readonly List<ConfigEntryViewModel> _entries = new();
    private IConfigDocument? _doc;
    private Encoding _encoding = new UTF8Encoding(false);
    private string _path = "";
    private GamePreset? _preset;
    private string? _section;
    private bool _isOpen, _busy, _closeArmed;
    private DateTime _closeArmedAt;
    private string _message = "", _gameWarning = "";

    public InspectorViewModel(IConfigFileStore files, IRunningExeSource running, Func<IReadOnlyList<DetectedConfig>> detect,
        Func<string, string>? display = null, Func<DateTime>? now = null)
    {
        _files = files;
        _running = running;
        _detect = detect;
        _display = display ?? (p => p);
        _now = now ?? (() => DateTime.UtcNow);
        OpenDetectedCommand = new RelayCommand(async p => { if (p is DetectedConfigViewModel d) await OpenAsync(d.Path); });
        SelectSectionCommand = new RelayCommand(p => { if (p is SectionItem s) SelectSection(s); });
        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        RevertCommand = new RelayCommand(_ => Revert());
        CloseCommand = new RelayCommand(_ => Close());
    }

    public ObservableCollection<DetectedConfigViewModel> Detected { get; } = new();
    public bool HasDetected => Detected.Count > 0;
    public ICommand OpenDetectedCommand { get; }

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (!SetField(ref _isOpen, value)) return;
            OnPropertyChanged(nameof(IsClosed));
        }
    }

    public bool IsClosed => !_isOpen;
    public bool IsEmptyFile => _isOpen && _entries.Count == 0;

    public string FileName => _path.Length == 0 ? "" : Path.GetFileName(_path);
    public string PathText => _path.Length == 0 ? "" : _display(_path);

    public string CountsText
    {
        get
        {
            int keys = _entries.Count;
            int sections = _entries.Select(e => e.Section).Distinct().Count();
            return (keys == 1 ? "1 key" : keys.ToString(Inv) + " keys") + " · " + (sections == 1 ? "1 section" : sections.ToString(Inv) + " sections");
        }
    }

    public string PresetBadge => _preset is null ? "" : _preset.Name.ToUpperInvariant() + " PRESET";
    public bool HasPreset => _preset is not null;

    public ObservableCollection<SectionItem> Sections { get; } = new();
    public ObservableCollection<ConfigSectionViewModel> Groups { get; } = new();
    public ICommand SelectSectionCommand { get; }

    public int ChangedCount => _entries.Count(e => e.IsDirty);

    public string SaveText => ChangedCount switch
    {
        0 => "Save",
        1 => "Save 1 change",
        int n => "Save " + n.ToString(Inv) + " changes",
    };

    public bool CanSave => ChangedCount > 0 && !_busy;
    public bool CanRevert => ChangedCount > 0 && !_busy;
    public ICommand SaveCommand { get; }
    public ICommand RevertCommand { get; }

    public string CloseText => _closeArmed ? "Confirm close" : "Close";
    public ICommand CloseCommand { get; }

    public string Message
    {
        get => _message;
        private set
        {
            if (!SetField(ref _message, value)) return;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public string StatusText => _message.Length > 0 ? _message : "A .bak backup is made on save";

    public string GameWarning
    {
        get => _gameWarning;
        private set
        {
            if (!SetField(ref _gameWarning, value)) return;
            OnPropertyChanged(nameof(ShowGameWarning));
        }
    }

    public bool ShowGameWarning => _gameWarning.Length > 0;

    public async Task LoadDetectedAsync()
    {
        IReadOnlyList<DetectedConfig> found;
        try { found = await Task.Run(_detect); }
        catch { found = Array.Empty<DetectedConfig>(); }

        Detected.Clear();
        foreach (DetectedConfig config in found)
            Detected.Add(new DetectedConfigViewModel(config, _display(config.Path), GamePresets.For(config.Path) is not null));
        OnPropertyChanged(nameof(HasDetected));
    }

    public async Task OpenAsync(string path)
    {
        if (_busy) return;
        if (_isOpen && ChangedCount > 0)
        {
            Message = "Save or revert your changes to " + FileName + " first.";
            return;
        }
        _busy = true;
        try
        {
            string name = Path.GetFileName(path);
            ConfigReadResult read = await Task.Run(() => _files.Read(path));
            if (read.File is null)
            {
                Message = "Couldn't open " + name + ": " + read.Error;
                return;
            }
            IConfigDocument? doc = ConfigFormats.Open(path, read.File.Text);
            if (doc is null)
            {
                Message = "Couldn't read " + name + " — it isn't valid JSON.";
                return;
            }

            ClearEntries();
            _doc = doc;
            _encoding = read.File.Encoding;
            _path = path;
            _preset = GamePresets.For(path);
            for (int i = 0; i < doc.Entries.Count; i++)
            {
                ConfigEntry entry = doc.Entries[i];
                var vm = new ConfigEntryViewModel(i, entry, ValueTyping.ShapeFor(entry.Key, entry.Value, _preset));
                vm.PropertyChanged += OnEntryChanged;
                _entries.Add(vm);
            }
            _section = null;
            _closeArmed = false;
            BuildSections();
            ApplyView();
            Message = read.File.IsReadOnly ? name + " is read-only — clear Read-only in its properties to save."
                : doc.Format == ConfigFormat.Json && (read.File.Text.Contains("//") || read.File.Text.Contains("/*"))
                    ? "This file's comments will be removed on save (JSON has no comments)."
                    : "";
            IsOpen = true;
            RaiseFile();
            await CheckGameAsync();
        }
        finally
        {
            _busy = false;
            RaiseChanges();
        }
    }

    public async Task SaveAsync()
    {
        if (_doc is null || _busy || ChangedCount == 0) return;
        _busy = true;
        RaiseChanges();
        try
        {
            await CheckGameAsync();
            string[] written = _entries.Select(e => e.Text).ToArray();
            for (int i = 0; i < _entries.Count; i++)
            {
                ConfigEntryViewModel entry = _entries[i];
                if (_doc.Entries[entry.Index].Value != written[i]) _doc.Set(entry.Index, written[i]);
            }
            string text = _doc.Serialize();
            string path = _path;
            Encoding encoding = _encoding;
            string? error = await Task.Run(() => _files.Save(path, text, encoding));
            if (error is null)
            {
                for (int i = 0; i < _entries.Count; i++) _entries[i].MarkSaved(written[i]);
                _closeArmed = false;
                Message = "Saved · backup " + FileName + ".bak created";
            }
            else
            {
                Message = "Couldn't save: " + error;
            }
        }
        finally
        {
            _busy = false;
            RaiseChanges();
        }
    }

    private void Revert()
    {
        if (_busy) return;
        foreach (ConfigEntryViewModel entry in _entries) entry.Revert();
        _closeArmed = false;
        Message = "Changes discarded.";
        RaiseChanges();
    }

    private void Close()
    {
        if (!_isOpen || _busy) return;
        int changes = ChangedCount;
        if (changes > 0 && !_closeArmed)
        {
            _closeArmed = true;
            _closeArmedAt = _now();
            Message = "Close without saving " + (changes == 1 ? "1 change" : changes.ToString(Inv) + " changes") + "? Click Close again.";
            RaiseChanges();
            return;
        }
        if (_closeArmed && _now() - _closeArmedAt < ConfirmDelay) return;

        ClearEntries();
        _doc = null;
        _path = "";
        _preset = null;
        _section = null;
        _closeArmed = false;
        Sections.Clear();
        Groups.Clear();
        Message = "";
        GameWarning = "";
        IsOpen = false;
        RaiseFile();
        RaiseChanges();
    }

    public async Task RefreshGameWarningAsync()
    {
        if (_isOpen) await CheckGameAsync();
    }

    private async Task CheckGameAsync()
    {
        GamePreset? preset = _preset;
        if (preset is null)
        {
            GameWarning = "";
            return;
        }
        IReadOnlySet<string>? running;
        try { running = await Task.Run(() => _running.Read()); }
        catch { running = null; }
        bool on = running is not null && running.Any(n => string.Equals(n, preset.ProcessExe, StringComparison.OrdinalIgnoreCase)
            || (preset.AltProcessExe is { } alt && string.Equals(n, alt, StringComparison.OrdinalIgnoreCase)));
        GameWarning = on ? preset.Name + " is running — it may overwrite this file when it exits. Close the game before saving." : "";
    }

    private void SelectSection(SectionItem item)
    {
        _section = item.Name;
        foreach (SectionItem s in Sections) s.IsActive = ReferenceEquals(s, item);
        ApplyView();
    }

    private void BuildSections()
    {
        Sections.Clear();
        var all = new SectionItem(null) { IsActive = true };
        Sections.Add(all);
        foreach (string name in _entries.Select(e => e.Section).Distinct()) Sections.Add(new SectionItem(name));
    }

    private void ApplyView()
    {
        Groups.Clear();
        foreach (IGrouping<string, ConfigEntryViewModel> group in _entries
                     .Where(e => _section is null || e.Section == _section)
                     .GroupBy(e => e.Section))
        {
            Groups.Add(new ConfigSectionViewModel(group.Key, group.ToList()));
        }
    }

    private void ClearEntries()
    {
        foreach (ConfigEntryViewModel entry in _entries) entry.PropertyChanged -= OnEntryChanged;
        _entries.Clear();
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConfigEntryViewModel.IsDirty)) RaiseChanges();
    }

    private void RaiseChanges()
    {
        OnPropertyChanged(nameof(ChangedCount));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanRevert));
        OnPropertyChanged(nameof(CloseText));
    }

    private void RaiseFile()
    {
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(PathText));
        OnPropertyChanged(nameof(CountsText));
        OnPropertyChanged(nameof(PresetBadge));
        OnPropertyChanged(nameof(HasPreset));
        OnPropertyChanged(nameof(IsEmptyFile));
    }
}
