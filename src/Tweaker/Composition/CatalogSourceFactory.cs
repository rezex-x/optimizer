using System.Reflection;
using Tweaker.Core.Services;

namespace Tweaker.Composition;

public static class CatalogSourceFactory
{
    public static ICatalogSource Create() => new EmbeddedCatalogSource(Assembly.GetExecutingAssembly(), "Tweaker.catalog.json");
}
