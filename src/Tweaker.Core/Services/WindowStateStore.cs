using System.IO;
using System.Text.Json;

namespace Tweaker.Core.Services;

public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized);

public sealed class WindowStateStore
{
    private readonly string _path;

    public WindowStateStore(string path) => _path = path;

    public WindowPlacement? Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(_path))
                : null;
        }
        catch { return null; }
    }

    public void Save(WindowPlacement placement)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(placement));
        }
        catch { }
    }

    public static bool IsReasonablyOnScreen(WindowPlacement p,
        double screenLeft, double screenTop, double screenWidth, double screenHeight)
    {
        if (p.Width <= 0 || p.Height <= 0) return false;

        double interLeft = System.Math.Max(p.Left, screenLeft);
        double interTop = System.Math.Max(p.Top, screenTop);
        double interRight = System.Math.Min(p.Left + p.Width, screenLeft + screenWidth);
        double interBottom = System.Math.Min(p.Top + p.Height, screenTop + screenHeight);

        return (interRight - interLeft) >= 120 && (interBottom - interTop) >= 40;
    }
}
