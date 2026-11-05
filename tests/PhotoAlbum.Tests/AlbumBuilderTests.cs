using PhotoAlbum.Core.Album;
using PhotoAlbum.Core.Layout;
using PhotoAlbum.Core.Scanning;
using PhotoAlbum.Pdf;

namespace PhotoAlbum.Tests;

public class AlbumBuilderTests
{
    private static AlbumSettings Settings(
        int min = 1, int max = 4, bool chronological = true, double margin = 0.25, double spacing = 0.10) =>
        new(8, 10, LengthUnit.Inches, min, max, chronological, margin, spacing);

    private static Photograph Photo(int index, DateTime? captured = null, int width = 300, int height = 200) =>
        new($@"C:\photos\img{index:D3}.jpg", width, height, 1, captured, null,
            captured is null ? DateSource.None : DateSource.ExifDateTimeOriginal);

    private static List<Photograph> Photos(int count) =>
        Enumerable.Range(0, count).Select(i => Photo(i, new DateTime(2024, 1, 1).AddHours(i))).ToList();

    [Fact]
    public void Build_PlacesEveryPhotographExactlyOnce_InOrder_WithinLimits()
    {
        var photos = Photos(37);

        var result = AlbumBuilder.Build(photos, Settings(min: 2, max: 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(photos.Select(p => p.FilePath), result.Pages.SelectMany(p => p.Photographs).Select(p => p.FilePath));
        Assert.Equal(37, result.PhotographCount);
        Assert.All(result.Pages, p => Assert.InRange(p.Photographs.Count, 2, 5));
        Assert.All(result.Pages, p => Assert.Equal(p.Photographs.Count, p.Layout.Photos.Count));
    }

    [Fact]
    public void Build_EveryLayoutIsValid()
    {
        var result = AlbumBuilder.Build(Photos(40), Settings(min: 1, max: 9));

        Assert.True(result.IsSuccess);
        foreach (var page in result.Pages)
        {
            var ratios = page.Photographs.Select(p => p.AspectRatio).ToArray();
            Assert.Empty(LayoutValidator.Validate(page.Layout, ratios, result.Page));
        }
    }

    [Fact]
    public void Build_Alphabetical_OrdersByFilename()
    {
        var photos = new List<Photograph>
        {
            Photo(3, new DateTime(2020, 1, 1)), Photo(1, new DateTime(2022, 1, 1)), Photo(2, new DateTime(2021, 1, 1)),
        };

        var result = AlbumBuilder.Build(photos, Settings(max: 3, chronological: false));

        Assert.Equal(["img001.jpg", "img002.jpg", "img003.jpg"],
            result.Pages.SelectMany(p => p.Photographs).Select(p => p.FileName));
    }

    [Fact]
    public void Build_Chronological_KeepsPhotosFromTheSameTimeFrameTogether()
    {
        // Three photographs one day, then five photographs ten days later.
        var photos = new List<Photograph>();
        for (var i = 0; i < 3; i++)
        {
            photos.Add(Photo(i, new DateTime(2024, 6, 1, 10, 0, 0).AddSeconds(i * 10)));
        }

        for (var i = 3; i < 8; i++)
        {
            photos.Add(Photo(i, new DateTime(2024, 6, 11, 10, 0, 0).AddSeconds(i * 10)));
        }

        var result = AlbumBuilder.Build(photos, Settings(min: 2, max: 6));

        Assert.Equal([3, 5], result.Pages.Select(p => p.Photographs.Count));
    }

    [Fact]
    public void Build_ImpossibleCounts_ReportsAProblemInsteadOfBreakingLimits()
    {
        var result = AlbumBuilder.Build(Photos(5), Settings(min: 3, max: 4));

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot be split", result.Error);
        Assert.Empty(result.Pages);
    }

    [Fact]
    public void Build_UndatedPhotos_ProduceAWarningOnlyWhenChronological()
    {
        var photos = new List<Photograph> { Photo(1, new DateTime(2024, 1, 1)), Photo(2), Photo(3) };

        var chronological = AlbumBuilder.Build(photos, Settings(max: 3));
        var alphabetical = AlbumBuilder.Build(photos, Settings(max: 3, chronological: false));

        Assert.Contains("2 of 3", Assert.Single(chronological.Warnings));
        Assert.Empty(alphabetical.Warnings);
    }

    [Theory]
    [InlineData(0, 10, 1, 4, 0.25, 0.1)]
    [InlineData(8, -1, 1, 4, 0.25, 0.1)]
    [InlineData(8, 10, 0, 4, 0.25, 0.1)]
    [InlineData(8, 10, 3, 2, 0.25, 0.1)]
    [InlineData(8, 10, 1, 4, -0.1, 0.1)]
    [InlineData(8, 10, 1, 4, 0.25, -1)]
    [InlineData(8, 10, 1, 4, 4.5, 0.1)]
    [InlineData(double.NaN, 10, 1, 4, 0.25, 0.1)]
    public void Build_InvalidSettings_ReturnsAMessage(double w, double h, int min, int max, double margin, double spacing)
    {
        var settings = new AlbumSettings(w, h, LengthUnit.Inches, min, max, true, margin, spacing);

        var result = AlbumBuilder.Build(Photos(5), settings);

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Build_NoPhotographs_Fails()
    {
        Assert.False(AlbumBuilder.Build([], Settings()).IsSuccess);
    }

    [Fact]
    public void Build_SpacingTooLargeToLayOut_NamesThePageAndSuggestsAFix()
    {
        var result = AlbumBuilder.Build(Photos(8), new AlbumSettings(8, 10, LengthUnit.Inches, 8, 8, true, 0.25, 3));

        Assert.False(result.IsSuccess);
        Assert.Contains("Page 1", result.Error);
    }

    [Fact]
    public void Build_KeepsLandscapeAndPortraitPageSizesAsEntered()
    {
        var landscape = new AlbumSettings(10, 8, LengthUnit.Inches, 1, 4, true, 0.25, 0.1);

        var result = AlbumBuilder.Build(Photos(3), landscape);

        Assert.True(result.Page.Width > result.Page.Height);
    }

    [Fact]
    public void ScanBuildExport_EndToEnd_ProducesAPdfWithTheExpectedPages()
    {
        using var dir = new TempDirectory();
        for (var i = 0; i < 9; i++)
        {
            TestJpeg.Create(
                dir.Path, $"p{i}.jpg", i % 2 == 0 ? 60 : 40, i % 2 == 0 ? 40 : 60,
                orientation: i % 3 == 0 ? 6 : 1, captured: new DateTime(2024, 5, 1).AddMinutes(i));
        }

        File.WriteAllBytes(Path.Combine(dir.Path, "broken.jpg"), [0, 1, 2]);
        var scan = PhotoScanner.Scan(dir.Path);
        var album = AlbumBuilder.Build(scan.Photographs, Settings(min: 2, max: 4));
        var output = Path.Combine(dir.Path, "photo_album.pdf");

        var exported = PdfAlbumExporter.Export(album.Pages, album.Page, output);

        Assert.Equal(9, scan.Photographs.Count);
        Assert.Single(scan.Unreadable);
        Assert.Equal(album.Pages.Count, exported.PageCount);
        Assert.True(new FileInfo(output).Length > 0);
    }
}
