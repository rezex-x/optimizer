namespace Tweaker.Core.Services;

public interface ICatalogSource
{
    string Load();
    bool CanSave { get; }
    void Save(string json);
}
