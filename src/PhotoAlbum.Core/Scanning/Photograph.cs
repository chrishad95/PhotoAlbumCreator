namespace PhotoAlbum.Core.Scanning;

/// <summary>Where a photograph's date came from.</summary>
public enum DateSource
{
    None,
    ExifDateTimeOriginal,
    ExifDateTimeDigitized,
    ExifDateTime,
    FileModified,
}

/// <summary>Metadata for one photograph. Capture date and file timestamp are kept distinct.</summary>
/// <param name="FilePath">Original file path.</param>
/// <param name="Width">Stored pixel width, before EXIF orientation is applied.</param>
/// <param name="Height">Stored pixel height, before EXIF orientation is applied.</param>
/// <param name="Orientation">EXIF orientation value 1-8 (1 = normal).</param>
/// <param name="CaptureDate">Date taken, from EXIF only.</param>
/// <param name="FallbackTimestamp">File modification time, used only when no capture date exists.</param>
/// <param name="DateSource">Which date the photograph is ordered by.</param>
public sealed record Photograph(
    string FilePath,
    int Width,
    int Height,
    int Orientation,
    DateTime? CaptureDate,
    DateTime? FallbackTimestamp,
    DateSource DateSource)
{
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>True when EXIF orientation rotates the image by 90 degrees (values 5-8).</summary>
    public bool IsRotated => Orientation is >= 5 and <= 8;

    /// <summary>Width after applying EXIF orientation.</summary>
    public int DisplayWidth => IsRotated ? Height : Width;

    /// <summary>Height after applying EXIF orientation.</summary>
    public int DisplayHeight => IsRotated ? Width : Height;

    /// <summary>Date used for chronological ordering: capture date, else fallback timestamp.</summary>
    public DateTime? SortDate => CaptureDate ?? FallbackTimestamp;
}

/// <summary>A file that could not be read as an image.</summary>
public sealed record UnreadablePhoto(string FilePath, string Reason);

/// <summary>Outcome of scanning a directory.</summary>
public sealed record ScanResult(IReadOnlyList<Photograph> Photographs, IReadOnlyList<UnreadablePhoto> Unreadable)
{
    /// <summary>Photographs with no EXIF capture date (they rely on the file timestamp).</summary>
    public int WithoutCaptureDateCount => Photographs.Count(p => p.CaptureDate is null);
}
