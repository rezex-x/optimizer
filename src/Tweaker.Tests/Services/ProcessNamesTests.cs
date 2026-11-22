using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ProcessNamesTests
{
    [Fact]
    public void Process_names_read_like_task_manager_details()
    {
        Assert.Equal("System Idle Process", ProcessNames.Display(0, null));
        Assert.Equal("System", ProcessNames.Display(4, "System"));
        Assert.Equal("chrome.exe", ProcessNames.Display(4820, "chrome"));
        Assert.Equal("Registry", ProcessNames.Display(88, "Registry"));
        Assert.Equal("Memory Compression", ProcessNames.Display(2640, "Memory Compression"));
        Assert.Equal("PID 123", ProcessNames.Display(123, null));
    }
}
