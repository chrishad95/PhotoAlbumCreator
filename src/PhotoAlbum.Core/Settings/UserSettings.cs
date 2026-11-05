using System.Text.Json;
using System.Text.Json.Serialization;
using PhotoAlbum.Core.Layout;

namespace PhotoAlbum.Core.Settings;

/// <summary>
/// The last-used configuration remembered between launches. Lengths are in <see cref="Unit"/>.
/// Contains no secrets and no photograph data, only the user's own choices.
/// </summary>
public sealed record UserSettings(
    string? SourceDirectory,
    string? ExportDirectory,
    double PageWidth,
    double PageHeight,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] LengthUnit Unit,
    int MinPerPage,
    int MaxPerPage,
    bool Chronological,
    double Margin,
    double Spacing)
{
    public static UserSettings Defaults { get; } = new(null, null, 8, 10, LengthUnit.Inches, 1, 4, true, 0.25, 0.1);

    /// <summary>
    /// Replaces any value that is not usable with its default, so a hand-edited or damaged file can never put
    /// the app into a state the user could not have typed. Each field is checked on its own.
    /// </summary>
    public UserSettings Sanitize()
    {
        var d = Defaults;
        var unit = Enum.IsDefined(Unit) ? Unit : d.Unit;
        var min = MinPerPage >= 1 ? MinPerPage : d.MinPerPage;
        var max = MaxPerPage >= min ? MaxPerPage : Math.Max(min, d.MaxPerPage);

        return new UserSettings(
            Clean(SourceDirectory),
            Clean(ExportDirectory),
            IsPositive(PageWidth) ? PageWidth : d.PageWidth,
            IsPositive(PageHeight) ? PageHeight : d.PageHeight,
            unit,
            min,
            max,
            Chronological,
            IsNonNegative(Margin) ? Margin : d.Margin,
            IsNonNegative(Spacing) ? Spacing : d.Spacing);
    }

    private static bool IsPositive(double value) => double.IsFinite(value) && value > 0;

    private static bool IsNonNegative(double value) => double.IsFinite(value) && value >= 0;

    private static string? Clean(string? path) =>
        string.IsNullOrWhiteSpace(path) || path.Length > 32_767 || path.Contains('\0') ? null : path;
}

/// <summary>Reads and writes <see cref="UserSettings"/> as a small JSON file. Never throws for a bad or missing file.</summary>
public static class UserSettingsStore
{
    // A settings file is a few hundred bytes. Anything far larger is not ours, so it is ignored.
    private const long MaxFileBytes = 64 * 1024;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Returns the saved settings, or <see cref="UserSettings.Defaults"/> if the file is missing, unreadable or invalid.</summary>
    public static UserSettings Load(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return UserSettings.Defaults;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserSettings>(json, Options)?.Sanitize() ?? UserSettings.Defaults;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                       or NotSupportedException or ArgumentException)
        {
            return UserSettings.Defaults;
        }
    }

    /// <summary>
    /// Saves the settings. The file is written to a temporary name and then moved into place, so a crash while
    /// saving cannot leave a half-written file. Returns false when the file could not be written.
    /// </summary>
    public static bool Save(string path, UserSettings settings)
    {
        var temp = path + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temp, JsonSerializer.Serialize(settings.Sanitize(), Options));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or ArgumentException)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup of the temporary file.
            }

            return false;
        }
    }
}
