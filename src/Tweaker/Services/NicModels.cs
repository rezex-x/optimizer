using System.Collections.Generic;

namespace Tweaker.Services;

public sealed record NicProperty(string Keyword, string DisplayName, string Value, IReadOnlyList<ChoiceOption> Options);

public sealed record NicInfo(string AdapterName, IReadOnlyList<NicProperty> Properties);

public interface INicPropertySource
{
    NicInfo? Read(string pnpDeviceId);

    string? Set(string adapterName, string keyword, string value);
}

public interface IAdvancedBackup
{
    IReadOnlyDictionary<string, string> Originals { get; }
    bool Remember(string key, string original);
    void Forget(string key);
}
