using System;
using System.Collections.Generic;
using System.Linq;

namespace Tweaker.Services;

public static class ProfilePlanner
{
    public static ProfilePlan Plan(AppProfile profile, IReadOnlyList<ProcessSample> processes, IReadOnlyList<ServiceSample> services,
        int selfPid, string windowsDir, Func<string?, string?> companyOf)
    {
        var candidates = new List<ProcessSample>();
        foreach (ProcessSample p in processes)
        {
            if (p.Owner != "User") continue;
            if (ProcessRules.IsProtected(p, selfPid)) continue;
            if (ProcessRules.Kind(p, windowsDir) == ProcessKind.Windows) continue;
            if (profile.HasTrigger && ProfileRules.SameExe(p.Name, profile.Trigger)) continue;

            bool listed = profile.Apps.Any(a => ProfileRules.SameExe(p.Name, a));
            bool close = profile.Mode == ProfileMode.CloseList
                ? listed
                : !listed && !ProfileRules.IsAllowlistExempt(p.Name) && !ProfileRules.IsKeptVendor(companyOf(p.ImagePath));
            if (close) candidates.Add(p);
        }

        var closes = new List<PlannedClose>();
        foreach (IGrouping<string, ProcessSample> app in candidates.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            bool anyWindow = app.Any(p => p.HasWindow);
            foreach (ProcessSample p in app)
            {
                if (anyWindow && !p.HasWindow) continue;
                closes.Add(new PlannedClose(p.Pid, p.CreatedTicks, p.Name, p.HasWindow));
            }
        }

        var stops = services
            .Where(s => s.State == ServiceState.Running && s.AcceptsStop && !ServiceRules.IsProtected(s))
            .Where(s => profile.Services.Any(e => ProfileRules.MatchesService(e, s)))
            .Select(s => new PlannedStop(s.Name, s.DisplayName))
            .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProfilePlan(
            closes.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Pid).ToList(),
            stops);
    }
}
