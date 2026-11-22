using System.Collections.Generic;

namespace Tweaker.ViewModels;

public sealed record NvidiaGroup(string Name, IReadOnlyList<NvidiaSettingViewModel> Settings);
