using PhotoAlbum.Core.Layout;
using PhotoAlbum.Core.Scanning;
using PhotoAlbum.Png;
using SkiaSharp;

namespace PhotoAlbum.Tests;

public class PngAlbumExporterTests
{
    // 8 x 10 inches = 576 x 720 points.
    private static readonly PageSpec Spec = PageSpec.From(8, 10, 0.25, 0.10, LengthUnit.Inches);

    private static Photograph Scan(string path, int width, int height, int orientation = 1) =>
        new(path, width, height, orientation, null, null, DateSource.None);

    private static AlbumPage MakePage(params Photograph[] photos) => MakePage(Spec, photos);

    private static AlbumPage MakePage(PageSpec spec, params Photograph[] photos)
    {
        var ratios = photos.Select(p => p.AspectRatio).ToArray();
        return new AlbumPage(photos, LayoutEngine.CreateCandidates(ratios, spec).First());
    }

    private static SKColor PixelAt(string pngPath, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(pngPath);
        return bitmap.GetPixel(x, y);
    }

    private static bool IsRed(SKColor c) => c.Red > 200 && c.Green < 90 && c.Blue < 90;

    [Fact]
    public void PixelSize_IsPageSizeTimesDpi()
    {
        Assert.Equal((2400, 3000), PngAlbumExporter.PixelSize(Spec, 300));
        Assert.Equal((600, 750), PngAlbumExporter.PixelSize(Spec, 75));
        Assert.Equal((3000, 2400), PngAlbumExporter.PixelSize(PageSpec.From(10, 8, 0, 0, LengthUnit.Inches), 300));
        Assert.Equal((2480, 3508), PngAlbumExporter.PixelSize(PageSpec.From(210, 297, 0, 0, LengthUnit.Millimeters), 300));
    }

    [Fact]
    public void FilePathsFor_NumbersPagesWithPaddingSoTheySortInOrder()
    {
        var paths = PngAlbumExporter.FilePathsFor(@"C:\out", "album", 3);

        Assert.Equal(
            [@"C:\out\album_page_001.png", @"C:\out\album_page_002.png", @"C:\out\album_page_003.png"],
            paths);
        Assert.EndsWith("album_page_1200.png", PngAlbumExporter.FilePathsFor(@"C:\out", "album", 1200)[^1]);
    }

    [Fact]
    public void Export_WritesOnePngPerPage_AtTheRequestedPrintResolution()
    {
        using var dir = new TempDirectory();
        var a = Scan(TestJpeg.Create(dir.Path, "a.jpg", 600, 400), 600, 400);
        var b = Scan(TestJpeg.Create(dir.Path, "b.jpg", 400, 600), 400, 600);
        var output = Directory.CreateDirectory(Path.Combine(dir.Path, "out")).FullName;

        var result = PngAlbumExporter.Export([MakePage(a, b), MakePage(a)], Spec, output, "album", dpi: 300);

        Assert.Equal(2, result.PageCount);
        Assert.Equal((2400, 3000, 300), (result.PixelWidth, result.PixelHeight, result.Dpi));
        Assert.All(result.FilePaths, p => Assert.True(File.Exists(p)));
        foreach (var path in result.FilePaths)
        {
            using var codec = SKCodec.Create(path);
            Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
            Assert.Equal((2400, 3000), (codec.Info.Width, codec.Info.Height));
        }

        // Only the page images are left behind: no temporary files.
        Assert.Equal(2, Directory.GetFiles(output).Length);
    }

    [Fact]
    public void Export_KeepsLandscapeAndPortraitSizesAsEntered()
    {
        using var dir = new TempDirectory();
        var photo = Scan(TestJpeg.Create(dir.Path, "a.jpg", 60, 40), 60, 40);
        var landscape = PageSpec.From(10, 8, 0.25, 0.10, LengthUnit.Inches);

        var result = PngAlbumExporter.Export([MakePage(landscape, photo)], landscape, dir.Path, dpi: 50);

        Assert.Equal((500, 400), (result.PixelWidth, result.PixelHeight));
    }

    [Fact]
    public void Export_EmbedsThePrintResolutionInTheFile()
    {
        using var dir = new TempDirectory();
        var photo = Scan(TestJpeg.Create(dir.Path, "a.jpg", 60, 40), 60, 40);

        var result = PngAlbumExporter.Export([MakePage(photo)], Spec, dir.Path, dpi: 300);

        var bytes = File.ReadAllBytes(result.FilePaths[0]);
        var phys = bytes.AsSpan().IndexOf("pHYs"u8);
        Assert.True(phys > 0, "The file has no pHYs chunk.");

        static int Int32At(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
        Assert.Equal(11811, Int32At(bytes, phys + 4)); // 300 dots per inch = 11811 per metre
        Assert.Equal(11811, Int32At(bytes, phys + 8));
        Assert.Equal(1, bytes[phys + 12]);
    }

    [Fact]
    public void Export_ThePageIsWhite_AndThePhotographFillsItsPlacement()
    {
        using var dir = new TempDirectory();
        var photo = Scan(TestJpeg.Create(dir.Path, "a.jpg", 400, 400), 400, 400);
        var page = MakePage(photo);
        var result = PngAlbumExporter.Export([page], Spec, dir.Path, dpi: 100);
        var rect = page.Layout.Photos[0];
        var scale = 100 / 72.0;

        // The corner is inside the margin, so it is the white page; the middle of the photograph is not white.
        Assert.Equal(SKColors.White, PixelAt(result.FilePaths[0], 2, 2));
        var centre = PixelAt(result.FilePaths[0], (int)((rect.X + (rect.Width * 0.75)) * scale), (int)((rect.Y + (rect.Height * 0.75)) * scale));
        Assert.NotEqual(SKColors.White, centre);
        Assert.False(IsRed(centre));

        // The red quadrant is the top-left of the photograph.
        var topLeft = PixelAt(result.FilePaths[0], (int)((rect.X + (rect.Width * 0.25)) * scale), (int)((rect.Y + (rect.Height * 0.25)) * scale));
        Assert.True(IsRed(topLeft));
    }

    [Theory]
    [InlineData(1, 0.25, 0.25)] // normal: red stays at the top-left
    [InlineData(3, 0.75, 0.75)] // rotated 180: red moves to the bottom-right
    [InlineData(6, 0.75, 0.25)] // rotated 90 clockwise: red moves to the top-right
    [InlineData(8, 0.25, 0.75)] // rotated 90 counter-clockwise: red moves to the bottom-left
    public void Export_AppliesExifOrientation(int orientation, double fractionX, double fractionY)
    {
        using var dir = new TempDirectory();
        var path = TestJpeg.Create(dir.Path, "rot.jpg", 400, 400, orientation);
        var page = MakePage(Scan(path, 400, 400, orientation));
        var result = PngAlbumExporter.Export([page], Spec, dir.Path, dpi: 100);
        var rect = page.Layout.Photos[0];
        var scale = 100 / 72.0;

        var pixel = PixelAt(
            result.FilePaths[0],
            (int)((rect.X + (rect.Width * fractionX)) * scale),
            (int)((rect.Y + (rect.Height * fractionY)) * scale));

        Assert.True(IsRed(pixel), $"Expected red at ({fractionX}, {fractionY}) for orientation {orientation}, got {pixel}.");
    }

    [Fact]
    public void Export_DoesNotModifyTheOriginalPhotographs()
    {
        using var dir = new TempDirectory();
        var path = TestJpeg.Create(dir.Path, "a.jpg", 120, 80);
        var before = File.ReadAllBytes(path);

        PngAlbumExporter.Export([MakePage(Scan(path, 120, 80))], Spec, dir.Path, dpi: 50);

        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Export_ExistingFile_IsRefusedUnlessOverwriteIsAllowed_AndIsNotDamaged()
    {
        using var dir = new TempDirectory();
        var photo = Scan(TestJpeg.Create(dir.Path, "a.jpg", 60, 40), 60, 40);
        var target = PngAlbumExporter.FilePathsFor(dir.Path, "photo_album", 1)[0];
        File.WriteAllText(target, "existing");

        Assert.Throws<PngExportException>(() => PngAlbumExporter.Export([MakePage(photo)], Spec, dir.Path, dpi: 50));
        Assert.Equal("existing", File.ReadAllText(target));

        PngAlbumExporter.Export([MakePage(photo)], Spec, dir.Path, dpi: 50, overwrite: true);
        using var codec = SKCodec.Create(target);
        Assert.NotNull(codec);
    }

    [Fact]
    public void Export_UnreadablePhotograph_FailsWithAFriendlyMessage_AndLeavesNoPartialSet()
    {
        using var dir = new TempDirectory();
        var good = Scan(TestJpeg.Create(dir.Path, "good.jpg", 60, 40), 60, 40);
        var missing = Scan(Path.Combine(dir.Path, "missing.jpg"), 60, 40);
        var output = Directory.CreateDirectory(Path.Combine(dir.Path, "out")).FullName;

        var ex = Assert.Throws<PngExportException>(
            () => PngAlbumExporter.Export([MakePage(good), MakePage(good, missing)], Spec, output, dpi: 50));

        Assert.Contains("missing.jpg", ex.Message);
        Assert.DoesNotContain(dir.Path, ex.Message);
        Assert.Empty(Directory.GetFiles(output));
    }

    [Fact]
    public void Export_RejectsBadInput()
    {
        using var dir = new TempDirectory();
        var photo = Scan(TestJpeg.Create(dir.Path, "a.jpg", 60, 40), 60, 40);

        Assert.Throws<PngExportException>(() => PngAlbumExporter.Export([], Spec, dir.Path));
        Assert.Throws<PngExportException>(() =>
            PngAlbumExporter.Export([MakePage(photo)], Spec, Path.Combine(dir.Path, "nope"), dpi: 50));
        Assert.Throws<PngExportException>(() =>
            PngAlbumExporter.Export([MakePage(photo)], new PageSpec(0, 720, 0, 0), dir.Path));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PngAlbumExporter.Export([MakePage(photo)], Spec, dir.Path, dpi: 0));
        Assert.Throws<ArgumentException>(() =>
            PngAlbumExporter.Export([MakePage(photo)], Spec, dir.Path, baseName: @"..\evil", dpi: 50));
    }

    [Fact]
    public void Export_AbsurdlyLargePage_IsRefusedBeforeAnyMemoryIsAllocated()
    {
        using var dir = new TempDirectory();
        var photo = Scan(TestJpeg.Create(dir.Path, "a.jpg", 60, 40), 60, 40);
        var huge = PageSpec.From(200, 200, 0.25, 0.1, LengthUnit.Inches);

        var ex = Assert.Throws<PngExportException>(() =>
            PngAlbumExporter.Export([MakePage(huge, photo)], huge, dir.Path, dpi: 300));

        Assert.Contains("too large", ex.Message);
    }

    [Fact]
    public void Export_ReportsProgress_AndCancellationLeavesNothingBehind()
    {
        using var dir = new TempDirectory();
        var photo = Scan(TestJpeg.Create(dir.Path, "a.jpg", 60, 40), 60, 40);
        var pages = new[] { MakePage(photo), MakePage(photo), MakePage(photo) };
        var output = Directory.CreateDirectory(Path.Combine(dir.Path, "out")).FullName;

        var reported = new List<int>();
        PngAlbumExporter.Export(pages, Spec, output, dpi: 50, progress: new SyncProgress(reported.Add));
        Assert.Equal([1, 2, 3], reported);

        Directory.Delete(output, true);
        Directory.CreateDirectory(output);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            PngAlbumExporter.Export(pages, Spec, output, dpi: 50, cancellationToken: cts.Token));
        Assert.Empty(Directory.GetFiles(output));
    }

    private sealed class SyncProgress(Action<int> handler) : IProgress<int>
    {
        public void Report(int value) => handler(value);
    }
}
