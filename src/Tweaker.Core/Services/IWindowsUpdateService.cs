using Tweaker.Core.Catalog;

namespace Tweaker.Core.Services;

public record UpdateApplyResult(bool Success, string Message);

public interface IWindowsUpdateService
{
    UpdateApplyResult Apply(UpdatePreset preset);
    UpdatePreset DetectActive();
}
