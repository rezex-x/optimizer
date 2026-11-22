using System.Collections.Generic;

namespace Tweaker.Services;

public sealed class JsonDevicePowerBackup : IDevicePowerBackup
{
    private readonly JsonOriginals<bool> _store;

    public JsonDevicePowerBackup(string path) => _store = new JsonOriginals<bool>(path);

    public IReadOnlyDictionary<string, bool> Originals => _store.Snapshot();
    public void Remember(string instanceId, bool original) => _store.Remember(instanceId, original);
    public void Forget(string instanceId) => _store.Forget(instanceId);
}
