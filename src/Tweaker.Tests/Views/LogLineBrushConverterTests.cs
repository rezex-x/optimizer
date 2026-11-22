using Tweaker.Converters;
using Xunit;

namespace Tweaker.Tests.Views;

public class LogLineBrushConverterTests
{
    [Theory]
    [InlineData("2026-09-27 14:03:11  [Changes] Reverted: Disable Game DVR (queued apply not applied)", true)]
    [InlineData("2026-09-27 14:03:11  [Changes] Reverted 3 queued changes", true)]
    [InlineData("2026-09-27 14:03:11  [Tweaks] Reverted: Disable Game DVR", true)]
    [InlineData("2026-09-27 14:03:11  [Tweaks] ERROR applying X: boom", true)]
    [InlineData("2026-09-27 14:03:11  [Tweaks]     Teams — failed: in use", true)]
    [InlineData("14:03:11  Reverted: X", true)]
    [InlineData("2026-09-27 14:03:11  [Tweaks] Applied: Disable Game DVR", false)]
    [InlineData("2026-09-27 14:03:11  [Services] Stopped Print Spooler.", false)]
    [InlineData("", false)]
    public void Reverts_and_errors_are_red(string line, bool red)
    {
        Assert.Equal(red, LogLineBrushConverter.IsRevertOrError(line));
    }
}
