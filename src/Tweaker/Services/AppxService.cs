using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Tweaker.Core.Services;

namespace Tweaker.Services;

public enum AppxRemoval { Removed, NotInstalled, Failed }

public sealed record AppxRemovalResult(string PackageName, AppxRemoval Result, string Reason);

public interface IAppxService
{
    IReadOnlySet<string>? InstalledPackageNames();

    IReadOnlyList<AppxRemovalResult> RemoveMany(IReadOnlyList<string> packageNames);
}

public sealed class AppxService : IAppxService
{
    private static readonly TimeSpan RemovalTimeout = TimeSpan.FromSeconds(600);

    private static readonly (string Package, string Key)[] OrchestratorKeys =
    {
        ("Microsoft.OutlookForWindows", "OutlookUpdate"),
        ("Microsoft.Windows.DevHome", "DevHomeUpdate"),
    };

    private readonly IProcessRunner _runner;

    public AppxService(IProcessRunner runner) => _runner = runner;

    public IReadOnlySet<string>? InstalledPackageNames()
    {
        try
        {
            ProcessResult r = _runner.Run("powershell.exe",
                "-NoProfile -NonInteractive -Command \"$ProgressPreference = 'SilentlyContinue'; Get-AppxPackage -AllUsers | ForEach-Object { $_.Name }\"");
            return string.IsNullOrWhiteSpace(r.StdOut) ? null : ParseNames(r.StdOut);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<AppxRemovalResult> RemoveMany(IReadOnlyList<string> packageNames)
    {
        List<string> safe = packageNames.Where(IsSafeName).ToList();
        IReadOnlyList<AppxRemovalResult> ran = Array.Empty<AppxRemovalResult>();
        if (safe.Count > 0)
        {
            try
            {
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(BuildRemovalScript(safe)));
                ProcessResult r = _runner.Run("powershell.exe", "-NoProfile -NonInteractive -EncodedCommand " + encoded, RemovalTimeout);
                ran = ParseRemoval(safe, r);
            }
            catch (Exception ex)
            {
                ran = safe.Select(n => new AppxRemovalResult(n, AppxRemoval.Failed, ex.Message)).ToList();
            }
        }

        var results = new List<AppxRemovalResult>(packageNames.Count);
        int next = 0;
        foreach (string name in packageNames)
            results.Add(IsSafeName(name) ? ran[next++] : new AppxRemovalResult(name ?? "", AppxRemoval.Failed, "unsupported package name"));
        return results;
    }

    public static string BuildRemovalScript(IReadOnlyList<string> safeNames)
    {
        var sb = new StringBuilder();
        sb.Append("$ProgressPreference = 'SilentlyContinue'\n");
        sb.Append("$ErrorActionPreference = 'Continue'\n");
        sb.Append("$prov = @(Get-AppxProvisionedPackage -Online)\n");
        sb.Append("foreach ($n in @(").Append(string.Join(",", safeNames.Select(n => "'" + n + "'"))).Append(")) {\n");
        sb.Append("  $pk = @(Get-AppxPackage -AllUsers -Name $n)\n");
        sb.Append("  $pv = @($prov | Where-Object DisplayName -eq $n)\n");
        sb.Append("  if ($pk.Count -eq 0 -and $pv.Count -eq 0) { Write-Output ($n + '|missing|'); continue }\n");
        sb.Append("  $why = ''\n");
        sb.Append("  foreach ($p in $pv) { try { Remove-AppxProvisionedPackage -Online -PackageName $p.PackageName -ErrorAction Stop | Out-Null } catch { $why = $_.Exception.Message } }\n");
        sb.Append("  foreach ($p in $pk) { try { Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction Stop } catch { $why = $_.Exception.Message } }\n");
        sb.Append("  if (@(Get-AppxPackage -AllUsers -Name $n).Count -eq 0) { Write-Output ($n + '|removed|') }\n");
        sb.Append("  else { Write-Output ($n + '|failed|' + (($why -replace '[\\r\\n|]', ' ').Trim())) }\n");
        sb.Append("}\n");
        foreach ((string package, string key) in OrchestratorKeys)
        {
            if (!safeNames.Contains(package, StringComparer.OrdinalIgnoreCase)) continue;
            sb.Append(@"Remove-Item -Path 'HKLM:\SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\")
              .Append(key).Append("' -Recurse -Force -ErrorAction SilentlyContinue\n");
        }
        return sb.ToString();
    }

    public static IReadOnlyList<AppxRemovalResult> ParseRemoval(IReadOnlyList<string> names, ProcessResult result)
    {
        var byName = new Dictionary<string, (string Status, string Reason)>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in (result.StdOut ?? "").Split('\n'))
        {
            string[] parts = line.Trim().Split('|', 3);
            if (parts.Length < 2 || parts[0].Length == 0) continue;
            byName[parts[0]] = (parts[1].Trim().ToLowerInvariant(), parts.Length > 2 ? parts[2].Trim() : "");
        }

        string? stderrLine = (result.StdErr ?? "").Split('\n').Select(l => l.Trim())
            .FirstOrDefault(l => l.Length > 0 && !l.StartsWith("#<", StringComparison.Ordinal) && !l.StartsWith("<Objs", StringComparison.Ordinal));
        string noLine = stderrLine ?? "PowerShell exited with code " + result.ExitCode.ToString(CultureInfo.InvariantCulture);

        var results = new List<AppxRemovalResult>(names.Count);
        foreach (string name in names)
        {
            if (!byName.TryGetValue(name, out (string Status, string Reason) line))
            {
                results.Add(new AppxRemovalResult(name, AppxRemoval.Failed, noLine));
                continue;
            }
            results.Add(line.Status switch
            {
                "removed" => new AppxRemovalResult(name, AppxRemoval.Removed, ""),
                "missing" => new AppxRemovalResult(name, AppxRemoval.NotInstalled, ""),
                _ => new AppxRemovalResult(name, AppxRemoval.Failed, line.Reason.Length > 0 ? line.Reason : "still installed"),
            });
        }
        return results;
    }

    public static HashSet<string> ParseNames(string output) =>
        new(output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0), StringComparer.OrdinalIgnoreCase);

    public static bool IsSafeName(string name) =>
        !string.IsNullOrEmpty(name) && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
}
