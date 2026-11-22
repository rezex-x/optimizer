namespace Tweaker.Core.Services;

public record InstallResult(bool Success, int ExitCode, string Message);

public interface IWingetService
{
    bool IsAvailable();
    InstallResult Install(string wingetId);
}
