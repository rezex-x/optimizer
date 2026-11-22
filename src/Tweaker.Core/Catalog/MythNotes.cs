using System;
using System.Linq;

namespace Tweaker.Core.Catalog;

public static class MythNotes
{
    private sealed record Myth(string[] Keywords, string Note);

    private static readonly Myth[] Myths =
    {
        new(new[] { "hpet" },
            "Disabling HPET in Device Manager has no measured benefit on modern PCs: it only hides the device, and Windows already keeps time with the CPU's invariant TSC. Tweaker leaves it out."),
        new(new[] { "defender" },
            "Turning Windows Defender off has no measured gaming benefit and removes your protection. If an anti-cheat triggers scans, add a Defender exclusion for its files instead."),
        new(new[] { "windows update", "wuauserv" },
            "Disabling Windows Update blocks security patches, and Windows turns it back on anyway. Use the Updates tab to pause or defer updates instead."),
        new(new[] { "realtime", "real-time" },
            "Realtime process priority doesn't make games faster: it starves input, disk and driver threads and can freeze the PC. Tweaker leaves it out."),
        new(new[] { "spectre", "meltdown", "mitigations" },
            "Turning off CPU security mitigations showed no measurable gaming gain in testing and removes exploit protection. Tweaker leaves it out."),
        new(new[] { "dep", "data execution prevention" },
            "Turning off Data Execution Prevention removes a basic exploit protection for no measured gain. Tweaker leaves it out."),
        new(new[] { "largesystemcache" },
            "LargeSystemCache is a Windows XP-era setting that does nothing on Windows 10 and 11."),
        new(new[] { "irq8", "irq8priority" },
            "IRQ8Priority is a myth from the Windows NT era and has no effect."),
        new(new[] { "autotuning", "auto-tuning", "tcp optimizer", "receive window" },
            "Turning off TCP auto-tuning usually lowers download speed on modern Windows and doesn't help game latency."),
        new(new[] { "write cache", "write-cache", "cache flushing" },
            "Turning off write-cache buffer flushing risks file-system corruption on a crash or power cut, with no measured gaming gain. Tweaker leaves it out."),
        new(new[] { "ram cleaner", "memory cleaner", "clean ram", "standby list" },
            "RAM cleaners make Windows slower: the cache they flush has to be read from disk again."),
        new(new[] { "anti-lag+", "antilag+" },
            "AMD Anti-Lag+ was withdrawn after it got players banned in CS2 and Apex. Anti-Lag 2 is the safe successor."),
        new(new[] { "gaming dns", "dns ping", "dns latency" },
            "DNS can't lower in-game ping: it only looks up the server's address before you connect."),
    };

    public static string? For(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return null;
        string text = search.Trim();
        return Myths.FirstOrDefault(m => m.Keywords.Any(k => ContainsWord(text, k)))?.Note;
    }

    public static bool ContainsWord(string text, string keyword)
    {
        int from = 0;
        while (from <= text.Length - keyword.Length)
        {
            int i = text.IndexOf(keyword, from, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            int end = i + keyword.Length;
            bool before = i == 0 || !char.IsLetterOrDigit(text[i - 1]);
            bool after = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after) return true;
            from = i + 1;
        }
        return false;
    }
}
