using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Tweaker.Services;

public sealed class JsonOriginals<TValue>
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<string, TValue>? _map;

    public JsonOriginals(string path) => _path = path;

    public IReadOnlyDictionary<string, TValue> Snapshot()
    {
        lock (_gate) return new Dictionary<string, TValue>(Map(), StringComparer.OrdinalIgnoreCase);
    }

    public bool Remember(string key, TValue original)
    {
        lock (_gate)
        {
            Dictionary<string, TValue> map = Map();
            if (_map is null && File.Exists(_path)) return false;
            if (map.ContainsKey(key)) return true;
            map[key] = original;
            if (Save(map)) return true;
            map.Remove(key);
            return false;
        }
    }

    public void Forget(string key)
    {
        lock (_gate)
        {
            Dictionary<string, TValue> map = Map();
            if (_map is null && File.Exists(_path)) return;
            if (map.Remove(key)) Save(map);
        }
    }

    private Dictionary<string, TValue> Map()
    {
        if (_map is not null) return _map;
        if (!File.Exists(_path)) return _map = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);

        string text;
        try
        {
            text = File.ReadAllText(_path);
        }
        catch
        {
            return new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            Dictionary<string, TValue>? loaded = JsonSerializer.Deserialize<Dictionary<string, TValue>>(text);
            if (loaded is not null)
                return _map = new Dictionary<string, TValue>(loaded.Where(kv => kv.Value is not null), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
        }

        try { File.Copy(_path, _path + ".corrupt", overwrite: true); } catch { }
        return _map = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
    }

    private bool Save(Dictionary<string, TValue> map)
    {
        string temp = _path + ".tmp";
        try
        {
            string? dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(temp, JsonSerializer.Serialize(map, Options));
            File.Move(temp, _path, overwrite: true);
            return true;
        }
        catch
        {
            try { File.Delete(temp); } catch { }
            return false;
        }
    }
}

public sealed class JsonAdvancedBackup : IAdvancedBackup
{
    private readonly JsonOriginals<string> _store;

    public JsonAdvancedBackup(string path) => _store = new JsonOriginals<string>(path);

    public IReadOnlyDictionary<string, string> Originals => _store.Snapshot();
    public bool Remember(string key, string original) => _store.Remember(key, original);
    public void Forget(string key) => _store.Forget(key);
}
