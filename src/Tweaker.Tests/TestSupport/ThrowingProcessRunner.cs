using Tweaker.Core.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class ThrowingProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, string arguments)
        => throw new System.ComponentModel.Win32Exception("not found");
}
