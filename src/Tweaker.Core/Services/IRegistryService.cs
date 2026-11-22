using Microsoft.Win32;

namespace Tweaker.Core.Services;

public interface IRegistryService
{
    (bool exists, RegistryValueKind kind, object? value) Read(string hive, string path, string? name);
    void Set(string hive, string path, string? name, RegistryValueKind kind, object value);
    void DeleteValue(string hive, string path, string? name);
    bool KeyExists(string hive, string path);
}
