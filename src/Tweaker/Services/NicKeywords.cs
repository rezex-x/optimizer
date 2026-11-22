using System;
using System.Collections.Generic;

namespace Tweaker.Services;

public static class NicKeywords
{
    private static readonly string[] Ordered =
    {
        "*EEE", "AdvancedEEE", "EEELinkAdvertisement", "EnableGreenEthernet", "GigaLite", "PowerSavingMode", "ULPMode",
        "*InterruptModeration", "*FlowControl", "*LsoV2IPv4", "*LsoV2IPv6", "*RSS",
    };

    private const string WakeUp = "Off avoids wake-up latency.";

    private static readonly Dictionary<string, string> Hints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["*EEE"] = "Energy-Efficient Ethernet. " + WakeUp,
        ["AdvancedEEE"] = WakeUp,
        ["EEELinkAdvertisement"] = WakeUp,
        ["EnableGreenEthernet"] = WakeUp,
        ["GigaLite"] = "Off keeps full gigabit speed.",
        ["PowerSavingMode"] = WakeUp,
        ["ULPMode"] = "Ultra-low-power mode. " + WakeUp,
        ["*InterruptModeration"] = "Keep on: turning it off costs CPU for little measured gain.",
        ["*FlowControl"] = "Leave at default unless you have a reason.",
        ["*LsoV2IPv4"] = "No measured gaming effect.",
        ["*LsoV2IPv6"] = "No measured gaming effect.",
        ["*RSS"] = "Keep on: spreads network work across cores.",
    };

    public static bool IsSupported(string keyword) => Hints.ContainsKey(keyword);

    public static string HintFor(string keyword) => Hints.TryGetValue(keyword, out string? hint) ? hint : "";

    public static int Order(string keyword)
    {
        int i = Array.FindIndex(Ordered, k => string.Equals(k, keyword, StringComparison.OrdinalIgnoreCase));
        return i < 0 ? Ordered.Length : i;
    }
}
