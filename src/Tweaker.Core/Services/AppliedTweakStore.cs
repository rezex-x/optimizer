using System.Text.Json;

namespace Tweaker.Core.Services;

public interface IAppliedTweakStore
{
    bool IsApplied(string tweakId);
    void SetApplied(string tweakId, bool applied);
}

public sealed class InMemoryAppliedTweakStore : IAppliedTweakStore
{
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

    public bool IsApplied(string tweakId) => _ids.Contains(tweakId);

    public void SetApplied(string tweakId, bool applied)
    {
        if (applied) _ids.Add(tweakId);
        else _ids.Remove(tweakId);
    }
}

public sealed class AppliedTweakStore : IAppliedTweakStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private HashSet<string>? _ids;

    public AppliedTweakStore(string path) => _path = path;

    public bool IsApplied(string tweakId)
    {
        lock (_gate) return Load().Contains(tweakId);
    }

    public void SetApplied(string tweakId, bool applied)
    {
        lock (_gate)
        {
            HashSet<string> ids = Load();
            bool changed = applied ? ids.Add(tweakId) : ids.Remove(tweakId);
            if (changed) Save(ids);
        }
    }

    private HashSet<string> Load()
    {
        if (_ids is not null) return _ids;
        try
        {
            if (File.Exists(_path))
            {
                string[]? stored = JsonSerializer.Deserialize<string[]>(File.ReadAllText(_path));
                _ids = new HashSet<string>(stored ?? Array.Empty<string>(), StringComparer.Ordinal);
                return _ids;
            }
        }
        catch { }
        _ids = new HashSet<string>(StringComparer.Ordinal);
        return _ids;
    }

    private void Save(HashSet<string> ids)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(ids.OrderBy(i => i, StringComparer.Ordinal).ToArray()));
            File.Move(tmp, _path, overwrite: true);
        }
        catch { }
    }
}
