using Tweaker.Core.Models;
using Tweaker.Views;
using Xunit;

namespace Tweaker.Tests.Views;

public class RegFileParserTests
{
    [Fact]
    public void Parses_dword_and_string_values()
    {
        string reg = """
        Windows Registry Editor Version 5.00

        [HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers]
        "HwSchMode"=dword:00000002
        "Label"="fast"
        """;

        List<RegistryOp> ops = RegFileParser.Parse(reg);

        Assert.Equal(2, ops.Count);
        Assert.Equal("HKLM", ops[0].Hive);
        Assert.Equal("SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers", ops[0].Path);
        Assert.Equal("HwSchMode", ops[0].Name);
        Assert.Equal("DWord", ops[0].Kind);
        Assert.Equal(2, ops[0].Value.GetInt32());
        Assert.Equal("String", ops[1].Kind);
        Assert.Equal("fast", ops[1].Value.GetString());
    }
}
