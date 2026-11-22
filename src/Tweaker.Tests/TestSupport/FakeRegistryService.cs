using Microsoft.Win32;
using Tweaker.Core.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeRegistryService : IRegistryService
{
    public Dictionary<string, (RegistryValueKind kind, object value)> Values { get; } = new();
    public List<(string hive, string path, string? name, RegistryValueKind kind, object value)> Writes { get; } = new();
    public List<(string hive, string path, string? name)> Deletes { get; } = new();

    private static string Key(string hive, string path, string? name) => $"{hive}|{path}|{name}";

    public (bool exists, RegistryValueKind kind, object? value) Read(string hive, string path, string? name)
        => Values.TryGetValue(Key(hive, path, name), out var v) ? (true, v.kind, v.value) : (false, RegistryValueKind.Unknown, null);

    public void Set(string hive, string path, string? name, RegistryValueKind kind, object value)
    {
        Writes.Add((hive, path, name, kind, value));
        Values[Key(hive, path, name)] = (kind, value);
    }

    public void DeleteValue(string hive, string path, string? name) => Deletes.Add((hive, path, name));
    public bool KeyExists(string hive, string path) => true;
}
