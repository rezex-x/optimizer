using System.Collections;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tweaker.Resources;

public static class AppIconImages
{
    private static readonly Lazy<HashSet<string>> Bundled = new(ReadBundledNames);
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string FileName(string wingetId)
    {
        var sb = new StringBuilder(wingetId.Length + 4);
        foreach (char c in wingetId) sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        return sb.Append(".png").ToString();
    }

    public static bool Has(string wingetId) => Bundled.Value.Contains(FileName(wingetId).ToLowerInvariant());

    public static ImageSource? Get(string wingetId)
    {
        if (!Has(wingetId)) return null;
        lock (Cache)
        {
            if (Cache.TryGetValue(wingetId, out ImageSource? cached)) return cached;
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri("pack://application:,,,/tweaker;component/Resources/AppIcons/" + FileName(wingetId), UriKind.Absolute);
            image.DecodePixelWidth = 64;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            Cache[wingetId] = image;
            return image;
        }
    }

    private static HashSet<string> ReadBundledNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var assembly = typeof(AppIconImages).Assembly;
        string resourceName = assembly.GetName().Name + ".g.resources";
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return names;
        using var reader = new ResourceReader(stream);
        foreach (DictionaryEntry entry in reader)
        {
            string key = (string)entry.Key;
            const string prefix = "resources/appicons/";
            if (key.StartsWith(prefix, StringComparison.Ordinal)) names.Add(key.Substring(prefix.Length).ToLower(CultureInfo.InvariantCulture));
        }
        return names;
    }
}
