using Microsoft.Win32;

namespace Tweaker.Tests.TestSupport;

public sealed class RegistrySandbox : IDisposable
{
    public string RelativePath { get; }
    public string Hive => "HKCU";

    public RegistrySandbox()
    {
        RelativePath = $"Software\\TweakerTest\\{Guid.NewGuid():N}";
        Registry.CurrentUser.CreateSubKey(RelativePath).Dispose();
    }

    public void Dispose()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree("Software\\TweakerTest\\" + RelativePath.Split('\\').Last(), throwOnMissingSubKey: false);
        }
        catch
        {
        }
    }
}
