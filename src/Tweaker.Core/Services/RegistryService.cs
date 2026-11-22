using Microsoft.Win32;

namespace Tweaker.Core.Services;

public sealed class RegistryService : IRegistryService
{
    public (bool exists, RegistryValueKind kind, object? value) Read(string hive, string path, string? name)
    {
        using RegistryKey root = OpenRoot(hive);
        using RegistryKey? key = root.OpenSubKey(path);
        if (key is null)
            return (false, RegistryValueKind.Unknown, null);

        string valueName = name ?? "";
        object? value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null)
            return (false, RegistryValueKind.Unknown, null);

        RegistryValueKind kind = key.GetValueKind(valueName);
        return (true, kind, value);
    }

    public void Set(string hive, string path, string? name, RegistryValueKind kind, object value)
    {
        using RegistryKey root = OpenRoot(hive);
        using RegistryKey key = root.CreateSubKey(path, writable: true);
        key.SetValue(name ?? "", value, kind);
    }

    public void DeleteValue(string hive, string path, string? name)
    {
        using RegistryKey root = OpenRoot(hive);
        using RegistryKey? key = root.OpenSubKey(path, writable: true);
        key?.DeleteValue(name ?? "", throwOnMissingValue: false);
    }

    public bool KeyExists(string hive, string path)
    {
        using RegistryKey root = OpenRoot(hive);
        using RegistryKey? key = root.OpenSubKey(path);
        return key is not null;
    }

    private static RegistryKey OpenRoot(string hive) => hive.ToUpperInvariant() switch
    {
        "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
        "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
        "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
        "HKU" or "HKEY_USERS" => Registry.Users,
        "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
        _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, "Unknown hive")
    };
}
