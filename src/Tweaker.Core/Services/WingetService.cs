namespace Tweaker.Core.Services;

public sealed class WingetService : IWingetService
{
    private readonly IProcessRunner _runner;

    private const int AlreadyInstalled = unchecked((int)0x8A15002B);
    private const int NoApplicableUpgrade = unchecked((int)0x8A15010C);

    public WingetService(IProcessRunner runner) => _runner = runner;

    public bool IsAvailable()
    {
        try { return _runner.Run("winget", "--version").ExitCode == 0; }
        catch { return false; }
    }

    public InstallResult Install(string wingetId)
    {
        string args = $"install --id {wingetId} -e --silent " +
                      "--accept-source-agreements --accept-package-agreements";
        ProcessResult r = _runner.Run("winget", args);
        bool ok = r.ExitCode == 0 || r.ExitCode == AlreadyInstalled || r.ExitCode == NoApplicableUpgrade;
        string msg = ok ? "Installed." : $"winget exit {r.ExitCode}: {r.StdErr.Trim()}";
        return new InstallResult(ok, r.ExitCode, msg);
    }
}
