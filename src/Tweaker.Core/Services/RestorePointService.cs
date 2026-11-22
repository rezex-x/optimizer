namespace Tweaker.Core.Services;

public record RestorePointResult(bool Success, bool ProtectionDisabled, string Message);

public sealed class RestorePointService
{
    private readonly IProcessRunner _runner;

    public RestorePointService(IProcessRunner runner) => _runner = runner;

    public RestorePointResult Create(string description)
    {
        string safe = description.Replace("\"", "'").Replace("'", "''");
        string arguments =
            $"-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description '{safe}' -RestorePointType MODIFY_SETTINGS\"";

        ProcessResult result = _runner.Run("powershell.exe", arguments);

        if (result.ExitCode == 0)
            return new RestorePointResult(true, false, "Restore point created.");

        string combined = (result.StdOut + " " + result.StdErr).ToLowerInvariant();
        bool protectionDisabled = combined.Contains("system protection")
                                  || combined.Contains("turned off")
                                  || combined.Contains("disabled");

        string message = protectionDisabled
            ? "System Protection appears to be disabled. Enable it in System Properties > System Protection, then retry."
            : "Failed to create a restore point: " + result.StdErr.Trim();

        return new RestorePointResult(false, protectionDisabled, message);
    }
}
