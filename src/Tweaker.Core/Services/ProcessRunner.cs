using System.Diagnostics;
using System.Globalization;

namespace Tweaker.Core.Services;

public sealed class ProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, string arguments)
    {
        using var process = new Process { StartInfo = StartInfo(fileName, arguments) };
        process.Start();
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    public ProcessResult Run(string fileName, string arguments, TimeSpan timeout)
    {
        using var process = new Process { StartInfo = StartInfo(fileName, arguments) };
        process.Start();
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        if (process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            process.WaitForExit();
            return new ProcessResult(process.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
        }

        try { process.Kill(entireProcessTree: true); }
        catch { }
        string stdoutSoFar = "";
        try { if (stdoutTask.Wait(TimeSpan.FromSeconds(5))) stdoutSoFar = stdoutTask.Result; }
        catch { }
        return new ProcessResult(-1, stdoutSoFar,
            "timed out after " + timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " s");
    }

    private static ProcessStartInfo StartInfo(string fileName, string arguments) => new()
    {
        FileName = fileName,
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
}
