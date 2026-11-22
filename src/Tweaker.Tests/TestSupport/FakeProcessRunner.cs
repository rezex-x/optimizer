using Tweaker.Core.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string fileName, string arguments)> Calls { get; } = new();
    public ProcessResult NextResult { get; set; } = new(0, "", "");

    public ProcessResult Run(string fileName, string arguments)
    {
        Calls.Add((fileName, arguments));
        return NextResult;
    }
}
