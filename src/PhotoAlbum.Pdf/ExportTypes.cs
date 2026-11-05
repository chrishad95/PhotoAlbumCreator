namespace PhotoAlbum.Pdf;

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
