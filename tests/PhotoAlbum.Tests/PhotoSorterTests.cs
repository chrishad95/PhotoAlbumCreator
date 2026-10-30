using PhotoAlbum.Core.Scanning;
using PhotoAlbum.Core.Sorting;

namespace PhotoAlbum.Tests;

public class PhotoSorterTests
{
    private static Photograph Photo(string name, DateTime? captured = null, DateTime? fallback = null) =>
        new($@"C:\photos\{name}", 100, 100, 1, captured, fallback,
            captured is not null ? DateSource.ExifDateTimeOriginal :
            fallback is not null ? DateSource.FileModified : DateSource.None);

    [Fact]
    public void Alphabetical_IgnoresDatesAndCase()
    {
        var photos = new[]
        {
            Photo("b.jpg", new DateTime(2020, 1, 1)),
            Photo("C.jpg", new DateTime(2019, 1, 1)),
            Photo("a.jpg", new DateTime(2021, 1, 1)),
        };

        var sorted = PhotoSorter.Sort(photos, chronological: false);

        Assert.Equal(["a.jpg", "b.jpg", "C.jpg"], sorted.Select(p => p.FileName));
    }

    [Fact]
    public void Chronological_OldestFirst()
    {
        var photos = new[]
        {
            Photo("a.jpg", new DateTime(2021, 1, 1)),
            Photo("b.jpg", new DateTime(2019, 1, 1)),
            Photo("c.jpg", new DateTime(2020, 1, 1)),
        };

        var sorted = PhotoSorter.Sort(photos, chronological: true);

        Assert.Equal(["b.jpg", "c.jpg", "a.jpg"], sorted.Select(p => p.FileName));
    }

    [Fact]
    public void Chronological_EqualDates_BreakTiesByFilename()
    {
        var when = new DateTime(2020, 5, 5, 10, 0, 0);
        var photos = new[] { Photo("z.jpg", when), Photo("m.jpg", when), Photo("a.jpg", when) };

        var sorted = PhotoSorter.Sort(photos, chronological: true);

        Assert.Equal(["a.jpg", "m.jpg", "z.jpg"], sorted.Select(p => p.FileName));
    }

    [Fact]
    public void Chronological_UsesFallbackTimestampWhenNoCaptureDate_AndPutsUndatedLast()
    {
        var photos = new[]
        {
            Photo("undated.jpg"),
            Photo("fallback.jpg", fallback: new DateTime(2018, 1, 1)),
            Photo("captured.jpg", new DateTime(2020, 1, 1)),
        };

        var sorted = PhotoSorter.Sort(photos, chronological: true);

        Assert.Equal(["fallback.jpg", "captured.jpg", "undated.jpg"], sorted.Select(p => p.FileName));
    }

    [Fact]
    public void Sorting_DoesNotLoseOrDuplicatePhotographs()
    {
        var photos = Enumerable.Range(0, 50).Select(i => Photo($"{i:D3}.jpg", new DateTime(2020, 1, 1).AddDays(i % 7))).ToList();

        var sorted = PhotoSorter.Sort(photos, chronological: true);

        Assert.Equal(photos.Count, sorted.Count);
        Assert.Equal(photos.Select(p => p.FilePath).Order(), sorted.Select(p => p.FilePath).Order());
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(8, true)]
    public void Orientation5To8_SwapsDisplayDimensions(int orientation, bool swapped)
    {
        var photo = new Photograph(@"C:\p\x.jpg", 400, 300, orientation, null, null, DateSource.None);

        Assert.Equal(swapped ? 300 : 400, photo.DisplayWidth);
        Assert.Equal(swapped ? 400 : 300, photo.DisplayHeight);
    }
}
