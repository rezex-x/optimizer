using Tweaker.Core.Catalog;
using Tweaker.Core.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeWindowsUpdateService : IWindowsUpdateService
{
    public UpdatePreset Active { get; set; } = UpdatePreset.Default;
    public List<UpdatePreset> Applied { get; } = new();

    public UpdateApplyResult Apply(UpdatePreset preset) { Applied.Add(preset); Active = preset; return new UpdateApplyResult(true, "ok"); }
    public UpdatePreset DetectActive() => Active;
}
