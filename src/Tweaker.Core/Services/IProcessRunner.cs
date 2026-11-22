namespace Tweaker.Core.Services;

public record ProcessResult(int ExitCode, string StdOut, string StdErr);

public interface IProcessRunner
{
    ProcessResult Run(string fileName, string arguments);

    ProcessResult Run(string fileName, string arguments, TimeSpan timeout) => Run(fileName, arguments);
}
