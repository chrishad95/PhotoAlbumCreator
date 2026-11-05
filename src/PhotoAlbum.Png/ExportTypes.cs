namespace PhotoAlbum.Png;

/// <summary>Outcome of a successful PNG export.</summary>
public sealed record PngExportResult(string Directory, IReadOnlyList<string> FilePaths, int PixelWidth, int PixelHeight, int Dpi)
{
    public int PageCount => FilePaths.Count;
}

/// <summary>An export failure with a message that is safe to show to the user.</summary>
public sealed class PngExportException : Exception
{
    public PngExportException(string message)
        : base(message)
    {
    }

    public PngExportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
