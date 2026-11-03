using System.Globalization;
using System.Text;
using SkiaSharp;

namespace PhotoAlbum.Tests;

/// <summary>Creates small JPEG files for tests, optionally with hand-built EXIF orientation and capture date.</summary>
internal static class TestJpeg
{
    /// <summary>
    /// Writes a gray JPEG with a red top-left quadrant (so rotation is visible). When <paramref name="orientation"/>
    /// or <paramref name="captured"/> is given, an EXIF APP1 segment is inserted after the SOI marker.
    /// </summary>
    public static string Create(
        string directory, string fileName, int width, int height, int? orientation = null, DateTime? captured = null)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Gray);
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, width / 2f, height / 2f, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        var jpeg = data.ToArray();

        if (orientation is not null || captured is not null)
        {
            var exif = BuildExifSegment(orientation ?? 1, captured);
            jpeg = [.. jpeg.AsSpan(0, 2), .. exif, .. jpeg.AsSpan(2)];
        }

        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, jpeg);
        return path;
    }

    private static byte[] BuildExifSegment(int orientation, DateTime? captured)
    {
        var tiff = new List<byte>();

        void U16(int v)
        {
            tiff.Add((byte)(v >> 8));
            tiff.Add((byte)v);
        }

        void U32(int v)
        {
            U16(v >> 16);
            U16(v & 0xFFFF);
        }

        tiff.AddRange("MM"u8.ToArray());
        U16(0x002A);
        U32(8);

        var entries = captured is null ? 1 : 2;
        U16(entries);
        U16(0x0112); U16(3); U32(1); U16(orientation); U16(0); // Orientation (SHORT)

        var exifIfdOffset = 8 + 2 + (entries * 12) + 4;
        if (captured is not null)
        {
            U16(0x8769); U16(4); U32(1); U32(exifIfdOffset); // pointer to the Exif sub-IFD
        }

        U32(0); // no next IFD

        if (captured is not null)
        {
            var textOffset = exifIfdOffset + 2 + 12 + 4;
            U16(1);
            U16(0x9003); U16(2); U32(20); U32(textOffset); // DateTimeOriginal (ASCII, 20 bytes)
            U32(0);
            tiff.AddRange(Encoding.ASCII.GetBytes(captured.Value.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture)));
            tiff.Add(0);
        }

        var payload = new List<byte>();
        payload.AddRange("Exif\0\0"u8.ToArray());
        payload.AddRange(tiff);

        var length = payload.Count + 2;
        var segment = new List<byte> { 0xFF, 0xE1, (byte)(length >> 8), (byte)length };
        segment.AddRange(payload);
        return [.. segment];
    }
}

/// <summary>A temporary directory that is deleted when disposed.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PhotoAlbumTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }
}
