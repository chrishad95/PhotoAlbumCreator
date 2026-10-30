using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;

namespace PhotoAlbum.Core.Scanning;

/// <summary>Discovers JPEG files in a directory (non-recursive) and reads their metadata. Never modifies files.</summary>
public static class PhotoScanner
{
    private const string UnreadableReason = "The file could not be read as an image.";

    public static ScanResult Scan(string directory, CancellationToken cancellationToken = default)
    {
        var photos = new List<Photograph>();
        var unreadable = new List<UnreadablePhoto>();

        foreach (var path in System.IO.Directory.EnumerateFiles(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ext = Path.GetExtension(path);
            if (!ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var photo = TryRead(path);
            if (photo is null)
            {
                unreadable.Add(new UnreadablePhoto(path, UnreadableReason));
            }
            else
            {
                photos.Add(photo);
            }
        }

        return new ScanResult(photos, unreadable);
    }

    private static Photograph? TryRead(string path)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);

            var jpeg = directories.OfType<JpegDirectory>().FirstOrDefault();
            if (jpeg is null ||
                !jpeg.TryGetInt32(JpegDirectory.TagImageWidth, out var width) ||
                !jpeg.TryGetInt32(JpegDirectory.TagImageHeight, out var height) ||
                width <= 0 || height <= 0)
            {
                return null;
            }

            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            var orientation = 1;
            if (ifd0 is not null &&
                ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out var o) &&
                o is >= 1 and <= 8)
            {
                orientation = o;
            }

            var (captured, source) = ReadCaptureDate(ifd0, subIfd);

            DateTime? fallback = null;
            try
            {
                fallback = File.GetLastWriteTime(path);
            }
            catch (IOException)
            {
                // The fallback timestamp is optional.
            }

            if (captured is null && fallback is not null)
            {
                source = DateSource.FileModified;
            }

            return new Photograph(path, width, height, orientation, captured, fallback, source);
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static (DateTime?, DateSource) ReadCaptureDate(ExifIfd0Directory? ifd0, ExifSubIfdDirectory? subIfd)
    {
        if (subIfd is not null)
        {
            if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original))
            {
                return (original, DateSource.ExifDateTimeOriginal);
            }

            if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var digitized))
            {
                return (digitized, DateSource.ExifDateTimeDigitized);
            }
        }

        if (ifd0 is not null && ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var modified))
        {
            return (modified, DateSource.ExifDateTime);
        }

        return (null, DateSource.None);
    }
}
