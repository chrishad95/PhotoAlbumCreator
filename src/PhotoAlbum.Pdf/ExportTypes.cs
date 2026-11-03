using PhotoAlbum.Core.Layout;
using PhotoAlbum.Core.Scanning;

namespace PhotoAlbum.Pdf;

/// <summary>One album page: its photographs in page order and where each one is placed.</summary>
public sealed record AlbumPage(IReadOnlyList<Photograph> Photographs, PageLayout Layout);

/// <summary>Outcome of a successful export.</summary>
public sealed record ExportResult(string FilePath, int PageCount);

/// <summary>An export failure with a message that is safe to show to the user.</summary>
public sealed class PdfExportException : Exception
{
    public PdfExportException(string message)
        : base(message)
    {
    }

    public PdfExportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
