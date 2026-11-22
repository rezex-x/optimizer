using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Tweaker.Core.Models;

namespace Tweaker.ViewModels;

public sealed record PendingChange(string Key, string Name, string ActionText, string Category, RiskLevel Risk, bool RequiresRestart,
    string Description, string Benefit, string Downsides, string? Warning, Func<Task> Execute, Action OnDiscard, bool RunOnce = false);

public sealed class PendingChangeFailedException : Exception
{
    public PendingChangeFailedException() : base("The change failed; the row already logged why.") { }
}

public sealed class PendingItemViewModel : ObservableObject
{
    private bool _isExpanded;

    public PendingItemViewModel(PendingChange change, ICommand remove)
    {
        Change = change;
        RemoveCommand = remove;
        ToggleCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
    }

    public PendingChange Change { get; }
    public string Key => Change.Key;
    public string Name => Change.Name;
    public string ActionText => Change.ActionText;
    public string Category => Change.Category;
    public RiskLevel Risk => Change.Risk;
    public bool RequiresRestart => Change.RequiresRestart;
    public string Description => Change.Description;
    public string Benefit => Change.Benefit;
    public string Downsides => Change.Downsides;

    public bool IsIrreversible => Change.RunOnce;

    public bool HasWarning => !string.IsNullOrWhiteSpace(Change.Warning) || Change.Risk == RiskLevel.High;
    public string WarningText => string.IsNullOrWhiteSpace(Change.Warning) ? "High risk — read the downsides before applying." : Change.Warning!;

    public bool IsExpanded { get => _isExpanded; set => SetField(ref _isExpanded, value); }
    public ICommand ToggleCommand { get; }
    public ICommand RemoveCommand { get; }
}

public sealed class PendingChangesViewModel : ObservableObject
{
    private readonly Action<string> _log;
    private bool _isApplying, _showAll;
    private string _progress = "";

    public PendingChangesViewModel(Action<string>? log = null)
    {
        _log = log ?? (_ => { });
        ApplyCommand = new RelayCommand(async _ => await ApplyAllAsync());
        DiscardCommand = new RelayCommand(_ => DiscardAll());
        RemoveCommand = new RelayCommand(p =>
        {
            if (p is not PendingItemViewModel item || !IsPending(item.Key)) return;
            Unstage(item.Key);
            _log(RevertedLine(item.Change));
        });
        ToggleAllDetailsCommand = new RelayCommand(_ => ShowAllDetails = !ShowAllDetails);
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsVisible));
            OnPropertyChanged(nameof(Summary));
        };
    }

    public event EventHandler? Changed;

    public ObservableCollection<PendingItemViewModel> Items { get; } = new();
    public int Count => Items.Count;
    public bool HasItems => Items.Count > 0;

    public bool IsVisible => HasItems || IsApplying;

    public string Summary
    {
        get
        {
            string queued = Count == 1 ? "1 change queued" : Count.ToString(CultureInfo.InvariantCulture) + " changes queued";
            int restart = Items.Count(i => i.RequiresRestart);
            if (restart == 0) return queued;
            return queued + " · " + (restart == 1 ? "1 needs a restart" : restart.ToString(CultureInfo.InvariantCulture) + " need a restart");
        }
    }

    public bool IsApplying
    {
        get => _isApplying;
        private set
        {
            if (!SetField(ref _isApplying, value)) return;
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    public bool CanEdit => !_isApplying;
    public string Progress { get => _progress; private set => SetField(ref _progress, value); }

    public bool ShowAllDetails
    {
        get => _showAll;
        set
        {
            if (!SetField(ref _showAll, value)) return;
            foreach (PendingItemViewModel item in Items) item.IsExpanded = value;
        }
    }

    public ICommand ApplyCommand { get; }
    public ICommand DiscardCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand ToggleAllDetailsCommand { get; }

    public bool IsPending(string key) => IndexOf(key) >= 0;

    public void Stage(PendingChange change)
    {
        int index = IndexOf(change.Key);
        var item = new PendingItemViewModel(change, RemoveCommand) { IsExpanded = index >= 0 ? Items[index].IsExpanded : _showAll };
        if (index >= 0) Items[index] = item;
        else Items.Add(item);
        RaiseChanged();
    }

    public void Unstage(string key)
    {
        int index = IndexOf(key);
        if (index < 0) return;
        PendingChange change = Items[index].Change;
        Items.RemoveAt(index);
        change.OnDiscard();
        RaiseChanged();
    }

    public void Withdraw(string key)
    {
        int index = IndexOf(key);
        if (index < 0) return;
        Items.RemoveAt(index);
        RaiseChanged();
    }

    public async Task ApplyAllAsync()
    {
        if (_isApplying || Items.Count == 0) return;
        IsApplying = true;
        List<string> keys = Items.Select(i => i.Key).ToList();
        int done = 0, failed = 0;
        try
        {
            for (int n = 0; n < keys.Count; n++)
            {
                int index = IndexOf(keys[n]);
                if (index < 0) continue;
                PendingChange change = Items[index].Change;
                Progress = "Applying " + (n + 1).ToString(CultureInfo.InvariantCulture) + " of " +
                           keys.Count.ToString(CultureInfo.InvariantCulture) + "…";
                Withdraw(change.Key);
                try
                {
                    await change.Execute();
                    done++;
                }
                catch (PendingChangeFailedException)
                {
                    failed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _log($"ERROR {change.ActionText.ToLowerInvariant()} {change.Name}: {ex.Message}");
                }
            }
        }
        finally
        {
            Progress = "";
            IsApplying = false;
        }
        string applied = "Applied " + (done == 1 ? "1 change" : done.ToString(CultureInfo.InvariantCulture) + " changes");
        _log(failed == 0 ? applied : applied + "; " + failed.ToString(CultureInfo.InvariantCulture) + " failed");
    }

    public void DiscardAll()
    {
        if (_isApplying || Items.Count == 0) return;
        List<PendingChange> changes = Items.Select(i => i.Change).ToList();
        Items.Clear();
        foreach (PendingChange change in changes) change.OnDiscard();
        RaiseChanged();
        foreach (PendingChange change in changes) _log(RevertedLine(change));
        _log("Reverted " + (changes.Count == 1 ? "1 queued change" : changes.Count.ToString(CultureInfo.InvariantCulture) + " queued changes"));
    }

    private static string RevertedLine(PendingChange change) =>
        "Reverted: " + change.Name + " (queued " + change.ActionText.ToLowerInvariant() + " not applied)";

    private int IndexOf(string key)
    {
        for (int i = 0; i < Items.Count; i++)
            if (string.Equals(Items[i].Key, key, StringComparison.Ordinal)) return i;
        return -1;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
