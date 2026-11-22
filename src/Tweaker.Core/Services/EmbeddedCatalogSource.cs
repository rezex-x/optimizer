using System.Reflection;

namespace Tweaker.Core.Services;

public sealed class EmbeddedCatalogSource : ICatalogSource
{
    private readonly Assembly _assembly;
    private readonly string _resourceName;

    public EmbeddedCatalogSource(Assembly assembly, string resourceName)
    {
        _assembly = assembly;
        _resourceName = resourceName;
    }

    public bool CanSave => false;

    public string Load()
    {
        using Stream? stream = _assembly.GetManifestResourceStream(_resourceName);
        if (stream is null)
            return "[]";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Save(string json)
        => throw new InvalidOperationException("The embedded catalog is read-only in the user build.");
}
