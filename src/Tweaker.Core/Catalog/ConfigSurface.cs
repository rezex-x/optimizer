namespace Tweaker.Core.Catalog;

public static class ConfigSurface
{
    public static IReadOnlySet<string> Categories { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Taskbar", "Start Menu", "Explorer", "Personalization", "Notifications", "Visual Effects",
        };

    public static bool IsConfig(string category) => Categories.Contains(category);
}
