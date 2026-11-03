using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PhotoAlbum.Core.Layout;

namespace PhotoAlbum.Pdf;

/// <summary>
/// Writes an album to a PDF: one PDF page per album page, at exactly the configured size, with no headers,
/// footers or page numbers. JPEG data is embedded as-is (no recompression); EXIF orientation is applied
/// with a drawing transform instead of re-encoding the image.
/// </summary>
public static class PdfAlbumExporter
{
    private const double SizeTolerancePoints = 0.05;

    /// <summary>
    /// Exports the pages and then re-opens the file to confirm the page count and page sizes.
    /// The PDF is written to a temporary file first, so a failed or cancelled export never leaves a partial
    /// file and never damages an existing one.
    /// </summary>
    /// <param name="overwrite">When false, an existing file at <paramref name="outputPath"/> is an error.</param>
    /// <param name="progress">Receives the number of pages written so far.</param>
    /// <exception cref="PdfExportException">The export failed; the message is safe to show to the user.</exception>
    public static ExportResult Export(
        IReadOnlyList<AlbumPage> pages,
        PageSpec pageSpec,
        string outputPath,
        bool overwrite = false,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (pages.Count == 0)
        {
            throw new PdfExportException("There are no pages to export.");
        }

        if (!(pageSpec.Width > 0 && pageSpec.Height > 0))
        {
            throw new PdfExportException("The page size must be greater than zero.");
        }

        foreach (var page in pages)
        {
            if (page.Photographs.Count != page.Layout.Photos.Count)
            {
                throw new ArgumentException("Each page needs exactly one placement per photograph.", nameof(pages));
            }
        }

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new PdfExportException("The output folder does not exist.");
        }

        if (File.Exists(fullPath) && !overwrite)
        {
            throw new PdfExportException("A file with that name already exists.");
        }

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            WriteDocument(pages, pageSpec, tempPath, progress, cancellationToken);
            Validate(tempPath, pages.Count, pageSpec);
            File.Move(tempPath, fullPath, overwrite);
            return new ExportResult(fullPath, pages.Count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PdfExportException("The PDF could not be written. Check the output location and try again.", ex);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void WriteDocument(
        IReadOnlyList<AlbumPage> pages, PageSpec pageSpec, string path, IProgress<int>? progress, CancellationToken ct)
    {
        using var document = new PdfDocument();
        document.Info.Title = "Photo Album";

        for (var i = 0; i < pages.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var pdfPage = document.AddPage();
            pdfPage.Width = XUnit.FromPoint(pageSpec.Width);
            pdfPage.Height = XUnit.FromPoint(pageSpec.Height);

            using (var gfx = XGraphics.FromPdfPage(pdfPage))
            {
                DrawPage(gfx, pages[i]);
            }

            progress?.Report(i + 1);
        }

        ct.ThrowIfCancellationRequested();
        document.Save(path);
    }

    private static void DrawPage(XGraphics gfx, AlbumPage page)
    {
        for (var i = 0; i < page.Photographs.Count; i++)
        {
            var photo = page.Photographs[i];

            XImage image;
            try
            {
                image = XImage.FromFile(photo.FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                           or NotImplementedException or ArgumentException)
            {
                throw new PdfExportException(
                    $"The photograph \"{photo.FileName}\" could not be read. It may have been moved, changed or damaged.", ex);
            }

            using (image)
            {
                var m = OrientationTransform.ForOrientation(photo.Orientation, page.Layout.Photos[i]);

                // Draw the stored image into a 1 x 1 square; the transform places, rotates and flips it.
                var state = gfx.Save();
                gfx.MultiplyTransform(new XMatrix(m.M11, m.M12, m.M21, m.M22, m.OffsetX, m.OffsetY), XMatrixOrder.Prepend);
                gfx.DrawImage(image, 0, 0, 1, 1);
                gfx.Restore(state);
            }
        }
    }

    private static void Validate(string path, int expectedPages, PageSpec pageSpec)
    {
        try
        {
            using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);

            if (document.PageCount != expectedPages)
            {
                throw new PdfExportException("The exported PDF has the wrong number of pages.");
            }

            foreach (var page in document.Pages)
            {
                if (Math.Abs(page.Width.Point - pageSpec.Width) > SizeTolerancePoints ||
                    Math.Abs(page.Height.Point - pageSpec.Height) > SizeTolerancePoints)
                {
                    throw new PdfExportException("The exported PDF pages do not match the requested size.");
                }
            }
        }
        catch (PdfExportException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or PdfSharp.PdfSharpException)
        {
            throw new PdfExportException("The exported PDF could not be verified.", ex);
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
