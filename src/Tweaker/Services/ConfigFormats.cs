using System;
using System.IO;
using System.Linq;

namespace Tweaker.Services;

public static class ConfigFormats
{
    public static IConfigDocument? Open(string path, string text)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".json":
                return JsonConfigDocument.Parse(text);
            case ".ini":
                return IniDocument.Parse(text);
            case ".cfg":
                return CfgDocument.Parse(text);
            case ".txt":
            case ".vdf":
                if (LooksLikeKeyValues(text)) return KeyValuesDocument.Parse(text);
                return LooksLikeIni(text) ? IniDocument.Parse(text) : CfgDocument.Parse(text);
        }
        string lead = text.TrimStart();
        if ((lead.StartsWith('{') || lead.StartsWith('[')) && JsonConfigDocument.Parse(text) is { } json) return json;
        if (LooksLikeKeyValues(text)) return KeyValuesDocument.Parse(text);
        if (LooksLikeIni(text)) return IniDocument.Parse(text);
        return CfgDocument.Parse(text);
    }

    private static bool LooksLikeKeyValues(string text)
    {
        foreach (string raw in text.Split('\n'))
        {
            string t = raw.Trim();
            if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal)) continue;
            return t.StartsWith('"') && text.Contains('{');
        }
        return false;
    }

    private static bool LooksLikeIni(string text) => text.Split('\n').Select(l => l.Trim()).Any(t =>
        (t.Length > 2 && t[0] == '[' && t[^1] == ']')
        || (t.Length > 0 && t[0] != ';' && t[0] != '#' && !t.StartsWith("//", StringComparison.Ordinal)
            && t.IndexOf('=') is > 0 and int eq && !t[..eq].Trim().Any(char.IsWhiteSpace)));
}
