using System.Windows;

namespace PhotoAlbum.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            AppLog.Error("Unhandled error", e.Exception);
            MessageBox.Show(
                "Something went wrong. Your photographs were not changed. Details were saved to the log file.",
                "Photo Album Page Creator",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };
    }
}
