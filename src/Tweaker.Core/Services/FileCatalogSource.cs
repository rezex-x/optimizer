namespace Tweaker.Core.Services;

public sealed class FileCatalogSource : ICatalogSource
{
    private readonly string _path;

    public FileCatalogSource(string path) => _path = path;

    public bool CanSave => true;

    public string Load() => File.Exists(_path) ? File.ReadAllText(_path) : "[]";

    public void Save(string json) => File.WriteAllText(_path, json);
}
