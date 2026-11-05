using PhotoAlbum.Core.Layout;
using PhotoAlbum.Core.Scanning;
using SkiaSharp;

namespace PhotoAlbum.Png;

/// <summary>
/// Renders each album page as a PNG at a print resolution (300 DPI by default, the usual requirement of photo-book
/// services). Photographs are decoded at full resolution one at a time, and EXIF orientation is applied with the
/// same <see cref="OrientationTransform"/> the PDF export and the preview use. Original files are never modified.
/// </summary>
public static class PngAlbumExporter
{
    public const int DefaultDpi = 300;

    // 150 million pixels is about 600 MB of memory for one page, which is already larger than any common book page.
    private const long MaxPagePixels = 150_000_000;

    private static readonly SKSamplingOptions DownscaleSampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>The file path each page will be written to: <c>{baseName}_page_001.png</c>, <c>_002</c>, and so on.</summary>
    public static IReadOnlyList<string> FilePathsFor(string directory, string baseName, int pageCount)
    {
        var digits = Math.Max(3, pageCount.ToString(System.Globalization.CultureInfo.InvariantCulture).Length);
        return Enumerable.Range(1, pageCount)
            .Select(n => Path.Combine(
                directory,
                $"{baseName}_page_{n.ToString("D" + digits, System.Globalization.CultureInfo.InvariantCulture)}.png"))
            .ToList();
    }

    /// <summary>The pixel size of one page at <paramref name="dpi"/>.</summary>
    public static (int Width, int Height) PixelSize(PageSpec page, int dpi) =>
        (Math.Max(1, (int)Math.Round(page.Width / LengthUnits.PointsPerInch * dpi)),
         Math.Max(1, (int)Math.Round(page.Height / LengthUnits.PointsPerInch * dpi)));

    /// <summary>
    /// Writes one PNG per page into <paramref name="directory"/>. Pages are written under temporary names and
    /// renamed only when every page succeeded, so a failure never leaves a partial set or replaces existing files.
    /// </summary>
    /// <param name="overwrite">When false, an existing file with a target name is an error.</param>
    /// <param name="progress">Receives the number of pages written so far.</param>
    /// <exception cref="PngExportException">The export failed; the message is safe to show to the user.</exception>
    public static PngExportResult Export(
        IReadOnlyList<AlbumPage> pages,
        PageSpec pageSpec,
        string directory,
        string baseName = "photo_album",
        int dpi = DefaultDpi,
        bool overwrite = false,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        ArgumentOutOfRangeException.ThrowIfLessThan(dpi, 1);

        if (pages.Count == 0)
        {
            throw new PngExportException("There are no pages to export.");
        }

        if (!(pageSpec.Width > 0 && pageSpec.Height > 0))
        {
            throw new PngExportException("The page size must be greater than zero.");
        }

        if (baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The base name contains characters that are not allowed in a file name.", nameof(baseName));
        }

        foreach (var page in pages)
        {
            if (page.Photographs.Count != page.Layout.Photos.Count)
            {
                throw new ArgumentException("Each page needs exactly one placement per photograph.", nameof(pages));
            }
        }

        var (width, height) = PixelSize(pageSpec, dpi);
        if ((long)width * height > MaxPagePixels)
        {
            throw new PngExportException(
                $"At {dpi} DPI each page would be {width} x {height} pixels, which is too large to create. Use a smaller page size.");
        }

        var fullDirectory = Path.GetFullPath(directory);
        if (!Directory.Exists(fullDirectory))
        {
            throw new PngExportException("The output folder does not exist.");
        }

        var targets = FilePathsFor(fullDirectory, baseName, pages.Count);
        if (!overwrite && targets.Any(File.Exists))
        {
            throw new PngExportException("A file with one of the page names already exists.");
        }

        var temps = new List<string>(pages.Count);
        try
        {
            for (var i = 0; i < pages.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var temp = Path.Combine(fullDirectory, $".{baseName}.{Guid.NewGuid():N}.tmp");
                temps.Add(temp);
                File.WriteAllBytes(temp, Render(pages[i], pageSpec, width, height, dpi));
                Verify(temp, width, height);
                progress?.Report(i + 1);
            }

            cancellationToken.ThrowIfCancellationRequested();
            for (var i = 0; i < temps.Count; i++)
            {
                File.Move(temps[i], targets[i], overwrite);
            }

            temps.Clear();
            return new PngExportResult(fullDirectory, targets, width, height, dpi);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PngExportException("The images could not be written. Check the output location and try again.", ex);
        }
        finally
        {
            foreach (var temp in temps)
            {
                TryDelete(temp);
            }
        }
    }

    private static byte[] Render(AlbumPage page, PageSpec pageSpec, int width, int height, int dpi)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var surface = SKSurface.Create(info)
            ?? throw new PngExportException("There is not enough memory to create a page image at this size.");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        var scaleX = width / pageSpec.Width;
        var scaleY = height / pageSpec.Height;

        for (var i = 0; i < page.Photographs.Count; i++)
        {
            var photo = page.Photographs[i];
            var rect = page.Layout.Photos[i];
            var target = new Rect(rect.X * scaleX, rect.Y * scaleY, rect.Width * scaleX, rect.Height * scaleY);

            using var image = Decode(photo);

            // Draw the stored image into a 1 x 1 square; the transform places, rotates and flips it.
            var m = OrientationTransform.ForOrientation(photo.Orientation, target);
            canvas.SetMatrix(new SKMatrix(
                (float)m.M11, (float)m.M21, (float)m.OffsetX,
                (float)m.M12, (float)m.M22, (float)m.OffsetY,
                0, 0, 1));
            canvas.DrawImage(image, new SKRect(0, 0, 1, 1), DownscaleSampling);
            canvas.ResetMatrix();
        }

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new PngExportException("The page image could not be encoded.");
        return PngDpi.Embed(data.ToArray(), dpi);
    }

    private static SKImage Decode(Photograph photo)
    {
        try
        {
            // The bitmap keeps the stored (unrotated) pixels; EXIF orientation is applied by the draw transform.
            using var bitmap = SKBitmap.Decode(photo.FilePath);
            if (bitmap is not null)
            {
                return SKImage.FromBitmap(bitmap);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PngExportException(UnreadableMessage(photo), ex);
        }

        throw new PngExportException(UnreadableMessage(photo));
    }

    private static string UnreadableMessage(Photograph photo) =>
        $"The photograph \"{photo.FileName}\" could not be read. It may have been moved, changed or damaged.";

    private static void Verify(string path, int width, int height)
    {
        using var codec = SKCodec.Create(path);
        if (codec is null || codec.Info.Width != width || codec.Info.Height != height)
        {
            throw new PngExportException("A page image was not written correctly.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup of the temporary file.
        }
    }
}
