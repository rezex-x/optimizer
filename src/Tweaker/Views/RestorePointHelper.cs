using System.Windows;
using Tweaker.Core.Services;

namespace Tweaker.Views;

internal static class RestorePointHelper
{
    public static void CreateWithFeedback()
    {
        var service = new RestorePointService(new ProcessRunner());
        RestorePointResult result = service.Create("Tweaker session " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        MessageBox.Show(result.Message, "Restore Point", MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
