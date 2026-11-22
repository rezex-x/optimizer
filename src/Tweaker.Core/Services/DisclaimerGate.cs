using System.IO;

namespace Tweaker.Core.Services;

public sealed class DisclaimerGate
{
    private readonly string _flagPath;

    public DisclaimerGate(string flagPath) => _flagPath = flagPath;

    public bool ShouldShow() => !File.Exists(_flagPath);

    public void Acknowledge()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_flagPath)!);
            File.WriteAllText(_flagPath, "ack");
        }
        catch { }
    }
}
