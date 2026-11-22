using System.Collections.Generic;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeConfirmationService : IConfirmationService
{
    public bool Result { get; set; } = true;
    public List<TweakConfirmRequest> SingleRequests { get; } = new();
    public List<(string Title, IReadOnlyList<TweakConfirmRequest> Items)> BatchRequests { get; } = new();

    public bool Confirm(TweakConfirmRequest request)
    {
        SingleRequests.Add(request);
        return Result;
    }

    public bool ConfirmBatch(string title, IReadOnlyList<TweakConfirmRequest> items)
    {
        BatchRequests.Add((title, items));
        return Result;
    }
}
