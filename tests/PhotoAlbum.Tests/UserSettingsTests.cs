using PhotoAlbum.Core.Layout;
using PhotoAlbum.Core.Settings;

namespace PhotoAlbum.Tests;

public sealed class UserSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pa_settings_" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static UserSettings Custom() =>
        new(@"C:\photos\trip", @"C:\exports", 210, 297, LengthUnit.Millimeters, 2, 6, false, 5, 2.5);

    [Fact]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        Assert.True(UserSettingsStore.Save(FilePath, Custom()));

        Assert.Equal(Custom(), UserSettingsStore.Load(FilePath));
    }

    [Fact]
    public void Save_CreatesMissingFolder_AndLeavesNoTemporaryFile()
    {
        Assert.True(UserSettingsStore.Save(FilePath, Custom()));

        Assert.Equal([FilePath], Directory.GetFiles(_directory));
    }

    [Fact]
    public void Save_OverwritesPreviousSettings()
    {
        UserSettingsStore.Save(FilePath, UserSettings.Defaults);
        UserSettingsStore.Save(FilePath, Custom());

        Assert.Equal(Custom(), UserSettingsStore.Load(FilePath));
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        Assert.Equal(UserSettings.Defaults, UserSettingsStore.Load(FilePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"PageWidth\": ")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"PageWidth\": \"wide\" }")]
    [InlineData("{ \"Unit\": \"Furlongs\" }")]
    public void Load_DamagedFile_ReturnsDefaults(string content)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, content);

        Assert.Equal(UserSettings.Defaults, UserSettingsStore.Load(FilePath));
    }

    [Fact]
    public void Load_OversizedFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, new string(' ', 200_000));

        Assert.Equal(UserSettings.Defaults, UserSettingsStore.Load(FilePath));
    }

    [Fact]
    public void Load_ReplacesUnusableValuesFieldByField_AndKeepsTheGoodOnes()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            FilePath,
            """
            {
              "SourceDirectory": "C:\\photos\\kept",
              "PageWidth": -4,
              "PageHeight": 12,
              "Unit": 99,
              "MinPerPage": 0,
              "MaxPerPage": 7,
              "Chronological": false,
              "Margin": -1,
              "Spacing": 0.5
            }
            """);

        var loaded = UserSettingsStore.Load(FilePath);

        var d = UserSettings.Defaults;
        Assert.Equal(@"C:\photos\kept", loaded.SourceDirectory);
        Assert.Equal(d.PageWidth, loaded.PageWidth);
        Assert.Equal(12, loaded.PageHeight);
        Assert.Equal(d.Unit, loaded.Unit);
        Assert.Equal(d.MinPerPage, loaded.MinPerPage);
        Assert.Equal(7, loaded.MaxPerPage);
        Assert.False(loaded.Chronological);
        Assert.Equal(d.Margin, loaded.Margin);
        Assert.Equal(0.5, loaded.Spacing);
    }

    [Fact]
    public void Sanitize_MaximumBelowMinimum_NeverLeavesAnImpossibleRange()
    {
        var result = (Custom() with { MinPerPage = 9, MaxPerPage = 3 }).Sanitize();

        Assert.True(result.MaxPerPage >= result.MinPerPage);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0.0)]
    public void Sanitize_NonFiniteOrZeroPageSize_FallsBackToDefault(double bad)
    {
        var result = (Custom() with { PageWidth = bad, PageHeight = bad }).Sanitize();

        Assert.Equal(UserSettings.Defaults.PageWidth, result.PageWidth);
        Assert.Equal(UserSettings.Defaults.PageHeight, result.PageHeight);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_BlankFolders_BecomeNull(string blank)
    {
        var result = (Custom() with { SourceDirectory = blank, ExportDirectory = blank }).Sanitize();

        Assert.Null(result.SourceDirectory);
        Assert.Null(result.ExportDirectory);
    }

    [Fact]
    public void Load_ReadOnlyDestination_SaveReturnsFalseInsteadOfThrowing()
    {
        // A file where the folder should be makes the folder impossible to create.
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "x");

        Assert.False(UserSettingsStore.Save(Path.Combine(blocker, "settings.json"), Custom()));
    }
}
