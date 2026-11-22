using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using Tweaker.Core.Services;

namespace Tweaker.Services;

public sealed class WmiNicPropertySource : INicPropertySource
{
    private const string Scope = @"root\StandardCimv2";
    private readonly IProcessRunner _runner;

    public WmiNicPropertySource(IProcessRunner runner) => _runner = runner;

    public NicInfo? Read(string pnpDeviceId)
    {
        try
        {
            string? name = null;
            if (!Query("SELECT Name, PnPDeviceID FROM MSFT_NetAdapter", mo =>
                {
                    if (name is null && string.Equals(mo["PnPDeviceID"]?.ToString(), pnpDeviceId, StringComparison.OrdinalIgnoreCase))
                        name = mo["Name"]?.ToString();
                }))
                return null;
            if (string.IsNullOrEmpty(name))
                return new NicInfo("", Array.Empty<NicProperty>());

            var properties = new List<NicProperty>();
            string escaped = name.Replace("\\", "\\\\").Replace("'", "\\'");
            bool ok = Query("SELECT RegistryKeyword, DisplayName, RegistryValue, DefaultRegistryValue, ValidRegistryValues, ValidDisplayValues " +
                            "FROM MSFT_NetAdapterAdvancedPropertySettingData WHERE Name = '" + escaped + "'", mo =>
            {
                string keyword = mo["RegistryKeyword"]?.ToString() ?? "";
                if (!NicKeywords.IsSupported(keyword)) return;
                string[] values = mo["ValidRegistryValues"] as string[] ?? Array.Empty<string>();
                string[] labels = mo["ValidDisplayValues"] as string[] ?? Array.Empty<string>();
                if (values.Length == 0 || values.Length != labels.Length) return;
                string current = (mo["RegistryValue"] as string[])?.FirstOrDefault() ?? "";
                if (current.Length == 0) current = mo["DefaultRegistryValue"]?.ToString() ?? "";
                properties.Add(new NicProperty(keyword, mo["DisplayName"]?.ToString() ?? keyword, current,
                    values.Zip(labels, (v, l) => new ChoiceOption(v, l)).ToList()));
            });
            if (!ok) return null;
            return new NicInfo(name, properties.OrderBy(p => NicKeywords.Order(p.Keyword)).ToList());
        }
        catch
        {
            return null;
        }
    }

    public string? Set(string adapterName, string keyword, string value)
    {
        try
        {
            if ((adapterName + keyword + value).Contains('"')) return "unsupported characters in the adapter name or value";
            if (adapterName.IndexOfAny(new[] { '[', ']', '*', '?' }) >= 0) return "unsupported characters in the adapter name";
            static string Quote(string s) => "'" + s.Replace("'", "''") + "'";
            ProcessResult result = _runner.Run("powershell.exe",
                "-NoProfile -Command \"Set-NetAdapterAdvancedProperty -Name " + Quote(adapterName) +
                " -RegistryKeyword " + Quote(keyword) + " -RegistryValue " + Quote(value) + " -ErrorAction Stop\"",
                TimeSpan.FromSeconds(120));
            if (result.ExitCode == 0) return null;
            string? line = (result.StdErr + "\n" + result.StdOut).Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            return line ?? "PowerShell exited with code " + result.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static bool Query(string wql, Action<ManagementObject> each)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(Scope), new ObjectQuery(wql),
                new EnumerationOptions { Timeout = TimeSpan.FromSeconds(10), ReturnImmediately = true, Rewindable = false });
            using ManagementObjectCollection results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo) each(mo);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
