using System.Collections.Generic;
using System.Globalization;

namespace Tweaker.Services;

public static class DeviceFormat
{
    public static string Description(string manufacturer, int errorCode)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(manufacturer)) parts.Add(manufacturer.Trim());
        if (errorCode != 0)
            parts.Add("Code " + errorCode.ToString(CultureInfo.InvariantCulture) + " · " + DeviceRules.ProblemText(errorCode));
        return string.Join(" · ", parts);
    }

    public static string GroupTitle(string group, int count) =>
        group.ToUpperInvariant() + " (" + count.ToString(CultureInfo.InvariantCulture) + ")";
}
