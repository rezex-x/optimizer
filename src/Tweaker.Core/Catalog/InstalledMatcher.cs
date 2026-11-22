using System.Text.RegularExpressions;

namespace Tweaker.Core.Catalog;

public static class InstalledMatcher
{
    private static readonly string[] Editions = { "Insiders", "Preview", "Beta", "Canary", "Nightly", "PTB" };

    public static IReadOnlySet<string> Match(IReadOnlyList<InstallApp> apps, string wingetListOutput,
        IEnumerable<string> uninstallDisplayNames, IEnumerable<string> runningProcessNames)
    {
        var tokens = new HashSet<string>(Regex.Split(wingetListOutput ?? "", @"\s+").Where(t => t.Length > 0), StringComparer.OrdinalIgnoreCase);
        List<string> truncated = tokens.Where(t => t.EndsWith('…') && t.Length > 6).Select(t => t.TrimEnd('…'))
            .Where(t => apps.Count(a => a.WingetId.StartsWith(t, StringComparison.OrdinalIgnoreCase)) == 1).ToList();
        List<string> names = uninstallDisplayNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
        var processes = new HashSet<string>(
            runningProcessNames.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Path.GetFileNameWithoutExtension(p.Trim())),
            StringComparer.OrdinalIgnoreCase);

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (InstallApp app in apps)
        {
            InstallDetectHint hint = InstallDetectHints.For(app);
            bool byName = names.Any(n => hint.DisplayNames.Any(d => hint.Exact ? n.Equals(d, StringComparison.OrdinalIgnoreCase) : IsNamed(n, d)));
            bool byId = tokens.Contains(app.WingetId)
                        || truncated.Any(t => app.WingetId.StartsWith(t, StringComparison.OrdinalIgnoreCase));
            if (byId || byName || hint.Processes.Any(processes.Contains))
                found.Add(app.WingetId);
        }
        return found;
    }

    private static bool IsNamed(string displayName, string name)
    {
        if (displayName.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
        bool followed = displayName.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase)
                        || (name.Length > 0 && char.IsDigit(name[^1]) && displayName.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase));
        if (!followed) return false;
        string rest = displayName[(name.Length + 1)..];
        return !Editions.Any(e => rest.Contains(e, StringComparison.OrdinalIgnoreCase));
    }
}
