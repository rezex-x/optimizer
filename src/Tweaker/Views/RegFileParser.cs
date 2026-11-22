using System.Globalization;
using System.Text.Json;
using Tweaker.Core.Models;

namespace Tweaker.Views;

public static class RegFileParser
{
    public static List<RegistryOp> Parse(string regText)
    {
        var ops = new List<RegistryOp>();
        string currentHive = "HKCU";
        string currentPath = "";

        foreach (string rawLine in regText.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("Windows Registry Editor", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                string full = line[1..^1];
                int slash = full.IndexOf('\\');
                string root = slash < 0 ? full : full[..slash];
                currentHive = MapHive(root);
                currentPath = slash < 0 ? "" : full[(slash + 1)..];
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq < 0) continue;

            string namePart = line[..eq].Trim().Trim('"');
            string valuePart = line[(eq + 1)..].Trim();

            var op = new RegistryOp { Hive = currentHive, Path = currentPath, Name = namePart };

            if (valuePart.StartsWith("dword:", StringComparison.OrdinalIgnoreCase))
            {
                int value = int.Parse(valuePart[6..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                op.Kind = "DWord";
                op.Value = JsonSerializer.SerializeToElement(value);
            }
            else
            {
                op.Kind = "String";
                op.Value = JsonSerializer.SerializeToElement(valuePart.Trim('"'));
            }

            ops.Add(op);
        }

        return ops;
    }

    private static string MapHive(string root) => root.ToUpperInvariant() switch
    {
        "HKEY_LOCAL_MACHINE" => "HKLM",
        "HKEY_CURRENT_USER" => "HKCU",
        "HKEY_CLASSES_ROOT" => "HKCR",
        "HKEY_USERS" => "HKU",
        "HKEY_CURRENT_CONFIG" => "HKCC",
        _ => root
    };
}
