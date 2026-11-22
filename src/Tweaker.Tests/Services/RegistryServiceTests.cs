using Microsoft.Win32;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class RegistryServiceTests
{
    [Fact]
    public void Set_then_Read_returns_dword_value()
    {
        using var sandbox = new RegistrySandbox();
        var svc = new RegistryService();

        svc.Set(sandbox.Hive, sandbox.RelativePath, "Answer", RegistryValueKind.DWord, 42);

        (bool exists, RegistryValueKind kind, object? value) = svc.Read(sandbox.Hive, sandbox.RelativePath, "Answer");

        Assert.True(exists);
        Assert.Equal(RegistryValueKind.DWord, kind);
        Assert.Equal(42, Convert.ToInt32(value));
    }

    [Fact]
    public void Read_missing_value_reports_not_exists()
    {
        using var sandbox = new RegistrySandbox();
        var svc = new RegistryService();

        (bool exists, _, _) = svc.Read(sandbox.Hive, sandbox.RelativePath, "Nope");

        Assert.False(exists);
    }

    [Fact]
    public void DeleteValue_removes_it()
    {
        using var sandbox = new RegistrySandbox();
        var svc = new RegistryService();
        svc.Set(sandbox.Hive, sandbox.RelativePath, "Temp", RegistryValueKind.String, "x");

        svc.DeleteValue(sandbox.Hive, sandbox.RelativePath, "Temp");

        Assert.False(svc.Read(sandbox.Hive, sandbox.RelativePath, "Temp").exists);
    }

    [Fact]
    public void KeyExists_true_for_created_key_false_for_missing()
    {
        using var sandbox = new RegistrySandbox();
        var svc = new RegistryService();

        Assert.True(svc.KeyExists(sandbox.Hive, sandbox.RelativePath));
        Assert.False(svc.KeyExists(sandbox.Hive, sandbox.RelativePath + "\\missing"));
    }
}
