using Tweaker.Core.Json;
using Tweaker.Core.Models;

namespace Tweaker.Core.Services;

public sealed class CatalogService
{
    private readonly ICatalogSource _source;
    private List<Tweak> _tweaks;

    public CatalogService(ICatalogSource source)
    {
        _source = source;
        _tweaks = TweakJson.DeserializeCatalog(_source.Load());
    }

    public List<Tweak> Tweaks => _tweaks;

    public bool CanEdit => _source.CanSave;

    public void Reload() => _tweaks = TweakJson.DeserializeCatalog(_source.Load());

    public IReadOnlyList<string> Categories()
        => _tweaks.Select(t => t.Category)
                  .Where(c => !string.IsNullOrWhiteSpace(c))
                  .Distinct()
                  .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                  .ToList();

    public IReadOnlyList<Tweak> InCategory(string category)
        => _tweaks.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

    public void AddOrUpdate(Tweak tweak)
    {
        if (!_source.CanSave)
            throw new InvalidOperationException("This catalog source is read-only.");

        int index = _tweaks.FindIndex(t => t.Id == tweak.Id);
        if (index >= 0)
            _tweaks[index] = tweak;
        else
            _tweaks.Add(tweak);

        _source.Save(TweakJson.SerializeCatalog(_tweaks));
    }
}
