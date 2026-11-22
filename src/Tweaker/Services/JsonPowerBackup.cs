using System.Collections.Generic;

namespace Tweaker.Services;

public interface IPowerBackup
{
    IReadOnlyDictionary<string, uint> Originals { get; }

    bool Remember(string key, uint original);

    void Forget(string key);
}

public sealed class JsonPowerBackup : IPowerBackup
{
    private readonly JsonOriginals<uint> _store;

    public JsonPowerBackup(string path) => _store = new JsonOriginals<uint>(path);

    public IReadOnlyDictionary<string, uint> Originals => _store.Snapshot();
    public bool Remember(string key, uint original) => _store.Remember(key, original);
    public void Forget(string key) => _store.Forget(key);
}
