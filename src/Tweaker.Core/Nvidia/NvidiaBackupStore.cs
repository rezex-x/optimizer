using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Tweaker.Core.Nvidia;

public sealed class NvidiaBackupStore
{
    private readonly string _path;
    public NvidiaBackupStore(string path) => _path = path;

    public uint? GetPrior(uint settingId)
        => Load().TryGetValue(settingId.ToString(), out uint v) ? v : (uint?)null;

    public void StorePriorIfAbsent(uint settingId, uint? priorValue)
    {
        if (priorValue is null) return;
        var map = Load();
        string key = settingId.ToString();
        if (map.ContainsKey(key)) return;
        map[key] = priorValue.Value;
        Save(map);
    }

    private Dictionary<string, uint> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            return JsonSerializer.Deserialize<Dictionary<string, uint>>(File.ReadAllText(_path)) ?? new();
        }
        catch { return new(); }
    }

    private void Save(Dictionary<string, uint> map)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(map));
    }
}
