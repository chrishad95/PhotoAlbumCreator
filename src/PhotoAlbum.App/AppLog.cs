using System.IO;

namespace PhotoAlbum.App;

/// <summary>Appends error details to a local log file so users never see stack traces.</summary>
internal static class AppLog
{
    private static readonly string LogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoAlbumCreator");

    public static string LogFile { get; } = Path.Combine(LogDirectory, "log.txt");

    public static void Error(string message, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(
                LogFile,
                $"{DateTime.Now:O} ERROR {message}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never crash the app.
        }
    }
}
