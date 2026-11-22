using System.Collections.Generic;
using Tweaker.Core.Models;

namespace Tweaker.Services;

public sealed record TweakConfirmRequest(
    string Name,
    string Category,
    RiskLevel Risk,
    bool RequiresRestart,
    string Description,
    string Benefit,
    string Downsides,
    string? Warning);

public interface IConfirmationService
{
    bool Confirm(TweakConfirmRequest request);

    bool ConfirmBatch(string title, IReadOnlyList<TweakConfirmRequest> items);
}
