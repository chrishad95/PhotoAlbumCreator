using PdfSharp.Pdf.IO;
using PhotoAlbum.Core.Layout;
using PhotoAlbum.Core.Scanning;
using PhotoAlbum.Pdf;

namespace PhotoAlbum.Tests;

public class PdfAlbumExporterTests
{
    // 8 x 10 inches = 576 x 720 points.
    private static readonly PageSpec Spec = PageSpec.From(8, 10, 0.25, 0.10, LengthUnit.Inches);

    private static Photograph Scan(string path, int width, int height, int orientation = 1) =>
        new(path, width, height, orientation, null, null, DateSource.None);

    private static AlbumPage MakePage(params Photograph[] photos)
    {
        var ratios = photos.Select(p => p.AspectRatio).ToArray();
        var layout = LayoutEngine.CreateCandidates(ratios, Spec).First();
        return new AlbumPage(photos, layout);
    }

    [Fact]
    public void Export_WritesOnePdfPagePerAlbumPage_AtTheExactSize()
    {
        using var dir = new TempDirectory();
        var a = Scan(TestJpeg.Create(dir.Path, "a.jpg", 60, 40), 60, 40);
        var b = Scan(TestJpeg.Create(dir.Path, "b.jpg", 40, 60), 40, 60);
        var c = Scan(TestJpeg.Create(dir.Path, "c.jpg", 50, 50), 50, 50);
        var output = Path.Combine(dir.Path, "photo_album.pdf");

        var result = PdfAlbumExporter.Export([MakePage(a, b), MakePage(c)], Spec, output);

        Assert.Equal(2, result.PageCount);
        Assert.True(File.Exists(output));
        using var pdf = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(2, pdf.PageCount);
        for (var i = 0; i < pdf.PageCount; i++)
        {
            Assert.Equal(576, pdf.Pages[i].Width.Point, 1);
            Assert.Equal(720, pdf.Pages[i].Height.Point, 1);
        }
    }

    [Fact]
    public void Export_KeepsLandscapeAndPortraitSizesAsEntered()
    {
        using var dir = new TempDirectory();
        var photo = Scan(TestJpeg.Create(dir.Path, "a.jpg", 60, 40), 60, 40);
        var landscape = PageSpec.From(10, 8, 0.25, 0.10, LengthUnit.Inches);
        var layout = LayoutEngine.CreateCandidates([photo.AspectRatio], landscape).First();
        var output = Path.Combine(dir.Path, "landscape.pdf");

        PdfAlbumExporter.Export([new AlbumPage([photo], layout)], landscape, output);

        using var pdf = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(720, pdf.Pages[0].Width.Point, 1);
        Assert.Equal(576, pdf.Pages[0].Height.Point, 1);
    }

    [Fact]
    public void Export_EmbedsTheOriginalJpegBytesWithoutRecompression()
    {
        using var dir = new TempDirectory();
        var path = TestJpeg.Create(dir.Path, "a.jpg", 120, 80);
        var original = File.ReadAllBytes(path);
        var output = Path.Combine(dir.Path, "out.pdf");

        PdfAlbumExporter.Export([MakePage(Scan(path, 120, 80))], Spec, output);

        Assert.True(File.ReadAllBytes(output).AsSpan().IndexOf(original) >= 0, "The JPEG data was re-encoded.");
    }

    [Fact]
    public void Export_AcceptsRotatedPhotographs()
    {
        using var dir = new TempDirectory();
        var path = TestJpeg.Create(dir.Path, "rot.jpg", 60, 40, orientation: 6);
        var output = Path.Combine(dir.Path, "out.pdf");

        var result = PdfAlbumExporter.Export([MakePage(Scan(path, 60, 40, orientation: 6))], Spec, output);

        Assert.Equal(1, result.PageCount);
    }

    [Fact]
    public void Export_ExistingFile_IsRefusedUnlessOverwriteIsAllowed_AndIsNotDamaged()
    {
        using var dir = new TempDirectory();
        var photo = Scan(TestJpeg.Create(dir.Path, "a.jpg", 60, 40), 60, 40);
        var output = Path.Combine(dir.Path, "photo_album.pdf");
        File.WriteAllText(output, "existing");

        Assert.Throws<PdfExportException>(() => PdfAlbumExporter.Export([MakePage(photo)], Spec, output));
        Assert.Equal("existing", File.ReadAllText(output));

        PdfAlbumExporter.Export([MakePage(photo)], Spec, output, overwrite: true);
        Assert.StartsWith("%PDF", File.ReadAllText(output)[..4] + string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_MissingPhoto_FailsWithFriendlyMessage_AndLeavesNoFiles()
    {
        using var dir = new TempDirectory();
        var good = Scan(TestJpeg.Create(dir.Path, "a.jpg", 60, 40), 60, 40);
        var missing = Scan(Path.Combine(dir.Path, "gone.jpg"), 60, 40);
        var output = Path.Combine(dir.Path, "photo_album.pdf");

        var ex = Assert.Throws<PdfExportException>(() => PdfAlbumExporter.Export([MakePage(good, missing)], Spec, output));

        Assert.Contains("gone.jpg", ex.Message);
        Assert.DoesNotContain(dir.Path, ex.Message);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp"));
    }

    [Fact]
    public void Export_MissingFolderOrNoPages_Fails()
    {
        using var dir = new TempDirectory();
        var photo = Scan(TestJpeg.Create(dir.Path, "a.jpg", 60, 40), 60, 40);

        Assert.Throws<PdfExportException>(() =>
            PdfAlbumExporter.Export([MakePage(photo)], Spec, Path.Combine(dir.Path, "nope", "x.pdf")));
        Assert.Throws<PdfExportException>(() =>
            PdfAlbumExporter.Export([], Spec, Path.Combine(dir.Path, "x.pdf")));
    }

    [Fact]
    public void Export_ReportsProgress_AndCanBeCancelledWithoutLeavingFiles()
    {
        using var dir = new TempDirectory();
        var photos = Enumerable.Range(0, 3)
            .Select(i => Scan(TestJpeg.Create(dir.Path, $"{i}.jpg", 60, 40), 60, 40)).ToArray();
        var pages = photos.Select(p => MakePage(p)).ToArray();

        var reported = new List<int>();
        PdfAlbumExporter.Export(pages, Spec, Path.Combine(dir.Path, "a.pdf"), progress: new SyncProgress(reported.Add));
        Assert.Equal([1, 2, 3], reported);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = Path.Combine(dir.Path, "cancelled.pdf");
        Assert.Throws<OperationCanceledException>(() =>
            PdfAlbumExporter.Export(pages, Spec, cancelled, cancellationToken: cts.Token));
        Assert.False(File.Exists(cancelled));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp"));
    }

    [Fact]
    public void Export_MismatchedPlacements_Throws()
    {
        var photo = Scan("x.jpg", 10, 10);
        var page = new AlbumPage([photo], new PageLayout("x", []));

        Assert.Throws<ArgumentException>(() => PdfAlbumExporter.Export([page], Spec, "x.pdf"));
    }

    private sealed class SyncProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }
}

public class OrientationTransformTests
{
    private static readonly Rect Box = new(100, 200, 40, 30);

    // Where the stored image's four corners (top-left, top-right, bottom-left, bottom-right) land.
    private static (double X, double Y)[] Corners(int orientation)
    {
        var m = OrientationTransform.ForOrientation(orientation, Box);
        return [m.Transform(0, 0), m.Transform(1, 0), m.Transform(0, 1), m.Transform(1, 1)];
    }

    [Theory]
    [InlineData(1, 100, 200)] // top-left stays top-left
    [InlineData(2, 140, 200)] // mirrored: top-left goes to top-right
    [InlineData(3, 140, 230)] // rotated 180: top-left goes to bottom-right
    [InlineData(4, 100, 230)] // flipped vertically: top-left goes to bottom-left
    [InlineData(5, 100, 200)] // transposed: top-left stays
    [InlineData(6, 140, 200)] // rotated 90 clockwise: top-left goes to top-right
    [InlineData(7, 140, 230)] // transverse: top-left goes to bottom-right
    [InlineData(8, 100, 230)] // rotated 90 counter-clockwise: top-left goes to bottom-left
    public void StoredTopLeftCorner_LandsWhereExifSaysItShould(int orientation, double x, double y)
    {
        var (cx, cy) = Corners(orientation)[0];

        Assert.Equal(x, cx, 6);
        Assert.Equal(y, cy, 6);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(0)]
    [InlineData(99)]
    public void EveryOrientation_CoversExactlyTheTargetRectangle(int orientation)
    {
        var corners = Corners(orientation);

        Assert.Equal(Box.X, corners.Min(c => c.X), 6);
        Assert.Equal(Box.Right, corners.Max(c => c.X), 6);
        Assert.Equal(Box.Y, corners.Min(c => c.Y), 6);
        Assert.Equal(Box.Bottom, corners.Max(c => c.Y), 6);
    }

    [Fact]
    public void UnknownOrientation_IsTreatedAsNormal()
    {
        Assert.Equal(Corners(1), Corners(0));
        Assert.Equal(Corners(1), Corners(99));
    }
}
