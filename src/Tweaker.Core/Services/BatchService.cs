using Tweaker.Core.Models;

namespace Tweaker.Core.Services;

public sealed class BatchService
{
    private readonly IProcessRunner _runner;

    public BatchService(IProcessRunner runner) => _runner = runner;

    public ProcessResult ApplyScript(Tweak tweak) => RunScript(tweak.ApplyScript);

    public ProcessResult RevertScript(Tweak tweak) => RunScript(tweak.RevertScript);

    private ProcessResult RunScript(string? script)
    {
        if (string.IsNullOrWhiteSpace(script))
            throw new InvalidOperationException("This tweak has no script for the requested action.");

        string arguments = $"/c \"{script}\"";
        return _runner.Run("cmd.exe", arguments);
    }
}
