using System.Collections.Generic;
using System.Windows;
using Tweaker.Views;

namespace Tweaker.Services;

public sealed class DialogConfirmationService : IConfirmationService
{
    public bool Confirm(TweakConfirmRequest request) => Show(new ConfirmDialog(request));

    public bool ConfirmBatch(string title, IReadOnlyList<TweakConfirmRequest> items)
        => Show(new ConfirmDialog(title, items));

    private static bool Show(ConfirmDialog dialog)
    {
        dialog.Owner = Application.Current?.MainWindow;
        return dialog.ShowDialog() == true;
    }
}
