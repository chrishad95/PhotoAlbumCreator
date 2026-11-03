using PhotoAlbum.Core.Scanning;

namespace PhotoAlbum.Tests;

public class PhotoScannerTests
{
    [Fact]
    public void Scan_FindsJpegs_IgnoresOtherFilesAndSubfolders_AndReportsUnreadable()
    {
        using var dir = new TempDirectory();
        TestJpeg.Create(dir.Path, "a.jpg", 40, 30);
        TestJpeg.Create(dir.Path, "B.JPEG", 30, 40);
        File.WriteAllText(Path.Combine(dir.Path, "notes.txt"), "not a photo");
        File.WriteAllBytes(Path.Combine(dir.Path, "picture.png"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(dir.Path, "broken.jpg"), [1, 2, 3, 4]);
        var sub = Directory.CreateDirectory(Path.Combine(dir.Path, "sub")).FullName;
        TestJpeg.Create(sub, "nested.jpg", 10, 10);

        var result = PhotoScanner.Scan(dir.Path);

        Assert.Equal(["a.jpg", "B.JPEG"], result.Photographs.Select(p => p.FileName).Order(StringComparer.OrdinalIgnoreCase));
        var broken = Assert.Single(result.Unreadable);
        Assert.Equal("broken.jpg", Path.GetFileName(broken.FilePath));
        Assert.False(string.IsNullOrWhiteSpace(broken.Reason));
    }

    [Fact]
    public void Scan_ReadsDimensionsOrientationAndCaptureDate()
    {
        using var dir = new TempDirectory();
        var taken = new DateTime(2023, 7, 4, 15, 30, 45);
        TestJpeg.Create(dir.Path, "exif.jpg", 40, 30, orientation: 6, captured: taken);

        var photo = Assert.Single(PhotoScanner.Scan(dir.Path).Photographs);

        Assert.Equal(40, photo.Width);
        Assert.Equal(30, photo.Height);
        Assert.Equal(6, photo.Orientation);
        Assert.Equal(30, photo.DisplayWidth);
        Assert.Equal(40, photo.DisplayHeight);
        Assert.Equal(taken, photo.CaptureDate);
        Assert.Equal(DateSource.ExifDateTimeOriginal, photo.DateSource);
    }

    [Fact]
    public void Scan_WithoutExif_UsesFileTimestampAsFallback_AndDefaultOrientation()
    {
        using var dir = new TempDirectory();
        var path = TestJpeg.Create(dir.Path, "plain.jpg", 20, 20);
        var modified = new DateTime(2019, 3, 2, 1, 2, 3);
        File.SetLastWriteTime(path, modified);

        var photo = Assert.Single(PhotoScanner.Scan(dir.Path).Photographs);

        Assert.Null(photo.CaptureDate);
        Assert.Equal(modified, photo.FallbackTimestamp);
        Assert.Equal(DateSource.FileModified, photo.DateSource);
        Assert.Equal(1, photo.Orientation);
        Assert.Equal(modified, photo.SortDate);
    }

    [Fact]
    public void Scan_NeverModifiesFiles()
    {
        using var dir = new TempDirectory();
        var path = TestJpeg.Create(dir.Path, "keep.jpg", 20, 20, orientation: 3);
        var before = File.ReadAllBytes(path);
        var stamp = File.GetLastWriteTimeUtc(path);

        PhotoScanner.Scan(dir.Path);

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Scan_CanBeCancelled()
    {
        using var dir = new TempDirectory();
        TestJpeg.Create(dir.Path, "a.jpg", 10, 10);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => PhotoScanner.Scan(dir.Path, cts.Token));
    }
}
