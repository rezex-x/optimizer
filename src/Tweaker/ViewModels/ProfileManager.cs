using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public interface IProfileState : INotifyPropertyChanged
{
    bool IsActive { get; }

    string BadgeText { get; }

    bool Manages(string serviceName, string displayName);
}

public sealed class ProfileManager : ObservableObject, IProfileState
{
    private readonly IProfileStore _store;
    private readonly IProcessSource _processes;
    private readonly IServiceSource _services;
    private readonly IProcessCloser _closer;
    private readonly IRunningExeSource _running;
    private readonly int _selfPid;
    private readonly string _windowsDir;
    private readonly Func<string?, string?> _companyOf;
    private readonly Func<DateTime> _now;
    private readonly TimeSpan _closeWait;
    private readonly List<AppProfile> _profiles;
    private readonly SemaphoreSlim _planGate = new(1, 1);
    private ProfileSession? _session;
    private string? _suppressedTrigger;
    private bool _busy, _ticking;
    private string _lastMessage = "";

    public ProfileManager(IProfileStore store, IProcessSource processes, IServiceSource services, IProcessCloser closer,
        IRunningExeSource running, int selfPid, string windowsDir, Func<string?, string?> companyOf, Func<DateTime>? now = null,
        TimeSpan? closeWait = null)
    {
        _store = store;
        _processes = processes;
        _services = services;
        _closer = closer;
        _running = running;
        _selfPid = selfPid;
        _windowsDir = windowsDir;
        _companyOf = companyOf;
        _now = now ?? (() => DateTime.UtcNow);
        _closeWait = closeWait ?? TimeSpan.FromSeconds(3);
        _profiles = store.LoadProfiles().ToList();
        _session = store.LoadSession();
    }

    public IReadOnlyList<AppProfile> Profiles => _profiles;
    public ProfileSession? Session => _session;
    public bool IsActive => _session is not null;
    public string ActiveId => _session?.ProfileId ?? "";
    public bool IsBusy => _busy;

    public string BadgeText => _session is null
        ? ""
        : _session.ProfileName.ToUpperInvariant() + " PROFILE ACTIVE — " + Count(_session.ClosedApps, "APP", "APPS") + " CLOSED";

    public string LastMessage
    {
        get => _lastMessage;
        private set
        {
            _lastMessage = value;
            OnPropertyChanged();
        }
    }

    public bool Manages(string serviceName, string displayName)
    {
        if (_session is null) return false;
        foreach (string entry in _session.ManagedServices)
        {
            string e = entry.Trim();
            if (e.Length > 0 && (string.Equals(e, serviceName, StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(e, displayName, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    public AppProfile? Find(string id) => _profiles.FirstOrDefault(p => p.Id == id);

    public void Save(AppProfile profile)
    {
        int i = _profiles.FindIndex(p => p.Id == profile.Id);
        AppProfile? old = i >= 0 ? _profiles[i] : null;
        if (i >= 0) _profiles[i] = profile;
        else _profiles.Add(profile);
        _store.SaveProfiles(_profiles);
        OnPropertyChanged(nameof(Profiles));

        if (_session is null || _session.ProfileId != profile.Id) return;
        ProfileSession updated = _session with
        {
            ProfileName = profile.Name,
            TriggerSeen = _session.TriggerSeen && old is not null && ProfileRules.SameExe(old.Trigger, profile.Trigger),
        };
        if (updated == _session) return;
        _session = updated;
        _store.SaveSession(_session);
        RaiseSession();
    }

    public AppProfile Create()
    {
        string name = "New profile";
        for (int n = 2; _profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)); n++)
            name = "New profile " + n.ToString(CultureInfo.InvariantCulture);
        var profile = new AppProfile(Guid.NewGuid().ToString("N"), name, "", "", false, true, ProfileMode.CloseList,
            Array.Empty<string>(), Array.Empty<string>(), false);
        Save(profile);
        return profile;
    }

    public bool Delete(string id)
    {
        AppProfile? profile = Find(id);
        if (profile is null || profile.IsBuiltIn || ActiveId == id) return false;
        _profiles.Remove(profile);
        _store.SaveProfiles(_profiles);
        OnPropertyChanged(nameof(Profiles));
        return true;
    }

    public async Task<IReadOnlyDictionary<string, string>> ServiceNamesAsync()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            ServiceSnapshot snapshot = await Task.Run(() => _services.Read());
            foreach (ServiceSample s in snapshot.Services) map[s.Name] = s.DisplayName;
        }
        catch
        {
        }
        return map;
    }

    public async Task<ProfilePlan> PreviewAsync(AppProfile profile)
    {
        await _planGate.WaitAsync();
        try
        {
            return await Task.Run(() => ProfilePlanner.Plan(profile, _processes.Read().Processes, _services.Read().Services,
                _selfPid, _windowsDir, _companyOf));
        }
        catch
        {
            return ProfilePlan.Nothing;
        }
        finally
        {
            _planGate.Release();
        }
    }

    public async Task<string> ActivateAsync(AppProfile profile, ProfilePlan plan, bool automatic)
    {
        if (_busy) return "Busy — try again in a moment.";
        if (_session is not null) return _session.ProfileName + " is already active — deactivate it first.";

        SetBusy(true);
        try
        {
            var provisional = new ProfileSession(profile.Id, profile.Name, _now(), automatic, automatic,
                Array.Empty<string>(), profile.Services.ToList(), 0);
            _store.SaveSession(provisional);

            Outcome outcome;
            await _planGate.WaitAsync();
            try
            {
                outcome = await Task.Run(() => Execute(plan,
                    stopped => _store.SaveSession(provisional with { StoppedServices = stopped })));
            }
            finally
            {
                _planGate.Release();
            }

            _session = provisional with { StoppedServices = outcome.Stopped, ClosedApps = outcome.Closed };
            _store.SaveSession(_session);
            RaiseSession();

            string text = profile.Name + " active — closed " + Count(outcome.Closed, "app", "apps")
                          + ", stopped " + Count(outcome.Stopped.Count, "service", "services") + ".";
            var problems = new List<string>();
            if (outcome.CloseFailed > 0) problems.Add(Count(outcome.CloseFailed, "app", "apps") + " didn't close");
            if (outcome.AskingToSave > 0)
                problems.Add(Count(outcome.AskingToSave, "app is", "apps are") + " asking to save");
            if (outcome.StopFailed > 0) problems.Add(Count(outcome.StopFailed, "service", "services") + " didn't stop");
            return problems.Count == 0 ? text : text + " " + string.Join(", ", problems) + ".";
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task<string> DeactivateAsync(bool restore)
    {
        if (_busy) return "Busy — try again in a moment.";
        ProfileSession? session = _session;
        if (session is null) return "No profile is active.";

        SetBusy(true);
        try
        {
            int restarted = 0;
            IReadOnlyList<string> failures = Array.Empty<string>();
            if (restore && session.StoppedServices.Count > 0)
                (restarted, failures) = await Task.Run(() => Restart(session.StoppedServices));

            _session = null;
            _store.SaveSession(null);
            RaiseSession();

            if (!restore || session.StoppedServices.Count == 0) return session.ProfileName + " deactivated.";
            string text = session.ProfileName + " deactivated — restarted " + Count(restarted, "service", "services") + ".";
            return failures.Count == 0 ? text : text + " " + string.Join("; ", failures) + ".";
        }
        finally
        {
            SetBusy(false);
        }
    }

    public Task<string> DeactivateByHandAsync()
    {
        AppProfile? profile = _session is null ? null : Find(_session.ProfileId);
        if (profile is not null && profile.HasTrigger && !_busy) _suppressedTrigger = ProfileRules.NormalizeExe(profile.Trigger);
        return DeactivateAsync(restore: true);
    }

    public async Task TickAsync()
    {
        if (_busy || _ticking || !NeedsWatching()) return;
        _ticking = true;
        try
        {
            IReadOnlySet<string>? running;
            try { running = await Task.Run(() => _running.Read()); }
            catch { running = null; }
            if (running is null || _busy) return;

            if (_suppressedTrigger is not null && !IsRunning(running, _suppressedTrigger)) _suppressedTrigger = null;

            if (_session is not null)
            {
                AppProfile? active = Find(_session.ProfileId);
                if (active is null || !active.HasTrigger) return;
                if (IsRunning(running, active.Trigger))
                {
                    if (!_session.TriggerSeen)
                    {
                        _session = _session with { TriggerSeen = true };
                        _store.SaveSession(_session);
                        OnPropertyChanged(nameof(Session));
                    }
                }
                else if (_session.TriggerSeen)
                {
                    LastMessage = await DeactivateAsync(active.RestoreServices);
                }
                return;
            }

            AppProfile? next = _profiles.FirstOrDefault(p =>
                IsAuto(p) && !ProfileRules.SameExe(p.Trigger, _suppressedTrigger) && IsRunning(running, p.Trigger));
            if (next is null) return;
            ProfilePlan plan = await PreviewAsync(next);

            if (_session is not null) return;
            AppProfile? current = Find(next.Id);
            if (current is null || current != next || !IsAuto(current)) return;
            LastMessage = await ActivateAsync(next, plan, automatic: true);
        }
        finally
        {
            _ticking = false;
        }
    }

    private bool NeedsWatching()
    {
        if (_suppressedTrigger is not null) return true;
        if (_session is not null) return Find(_session.ProfileId)?.HasTrigger == true;
        return _profiles.Any(IsAuto);
    }

    private static bool IsAuto(AppProfile p) => p.AutoActivate && p.HasTrigger && p.Mode == ProfileMode.CloseList;

    private static bool IsRunning(IReadOnlySet<string> running, string trigger)
    {
        string exe = ProfileRules.NormalizeExe(trigger);
        return exe.Length > 0 && running.Any(n => string.Equals(n, exe, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record Outcome(int Closed, int CloseFailed, int AskingToSave, List<string> Stopped, int StopFailed);

    private Outcome Execute(ProfilePlan plan, Action<IReadOnlyList<string>> onStopped)
    {
        int closed = 0, closeFailed = 0, askingToSave = 0, stopFailed = 0;
        var posted = new List<PlannedClose>();
        foreach (PlannedClose c in plan.Close.Where(c => c.Graceful))
        {
            string? error;
            try { error = _closer.CloseWindow(c.Pid, c.CreatedTicks); }
            catch (Exception ex) { error = ex.Message; }
            if (error is null) posted.Add(c);
            else closeFailed++;
        }
        foreach (PlannedClose c in plan.Close.Where(c => !c.Graceful))
        {
            if (KillOrError(c.Pid, c.CreatedTicks) is null) closed++;
            else closeFailed++;
        }

        if (posted.Count > 0)
        {
            var clock = Stopwatch.StartNew();
            ProcessSnapshot snapshot = ReadProcesses();
            while (!ReferenceEquals(snapshot, ProcessSnapshot.Empty)
                   && posted.Any(c => FindSample(snapshot, c) is not null)
                   && clock.Elapsed < _closeWait)
            {
                Thread.Sleep(500);
                snapshot = ReadProcesses();
            }

            if (ReferenceEquals(snapshot, ProcessSnapshot.Empty))
            {
                closed += posted.Count;
            }
            else
            {
                var killed = new Dictionary<int, string?>();
                foreach (PlannedClose c in posted)
                {
                    ProcessSample? sample = FindSample(snapshot, c);
                    if (sample is null) closed++;
                    else if (sample.HasWindow) askingToSave++;
                    else if (EndEveryInstance(snapshot, sample, killed)) closed++;
                    else closeFailed++;
                }
            }
        }

        var stopped = new List<string>();
        foreach (PlannedStop s in plan.Stop)
        {
            string? error;
            try { error = _services.Stop(s.Name); }
            catch (Exception ex) { error = ex.Message; }
            if (error is null)
            {
                stopped.Add(s.Name);
                onStopped(stopped.ToList());
            }
            else stopFailed++;
        }
        return new Outcome(closed, closeFailed, askingToSave, stopped, stopFailed);
    }

    private bool EndEveryInstance(ProcessSnapshot snapshot, ProcessSample target, Dictionary<int, string?> killed)
    {
        foreach (ProcessSample p in snapshot.Processes)
        {
            if (!string.Equals(p.Name, target.Name, StringComparison.OrdinalIgnoreCase)) continue;
            if (p.Owner != "User" || ProcessRules.IsProtected(p, _selfPid) || killed.ContainsKey(p.Pid)) continue;
            killed[p.Pid] = KillOrError(p.Pid, p.CreatedTicks);
        }
        return killed.TryGetValue(target.Pid, out string? error) && error is null;
    }

    private static ProcessSample? FindSample(ProcessSnapshot snapshot, PlannedClose c) =>
        snapshot.Processes.FirstOrDefault(p => p.Pid == c.Pid && p.CreatedTicks == c.CreatedTicks);

    private ProcessSnapshot ReadProcesses()
    {
        try { return _processes.Read(); }
        catch { return ProcessSnapshot.Empty; }
    }

    private string? KillOrError(int pid, long createdTicks)
    {
        try { return _processes.Kill(pid, createdTicks); }
        catch (Exception ex) { return ex.Message; }
    }

    private (int Restarted, IReadOnlyList<string> Failures) Restart(IReadOnlyList<string> names)
    {
        int restarted = 0;
        var failures = new List<string>();
        foreach (string name in names)
        {
            string? error;
            try { error = _services.Start(name); }
            catch (Exception ex) { error = ex.Message; }
            if (error is null) restarted++;
            else failures.Add(name + " didn't restart (" + error + ")");
        }
        return (restarted, failures);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        OnPropertyChanged(nameof(IsBusy));
    }

    private void RaiseSession()
    {
        OnPropertyChanged(nameof(Session));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(ActiveId));
        OnPropertyChanged(nameof(BadgeText));
    }

    private static string Count(int n, string one, string many) =>
        n.ToString(CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many);
}
