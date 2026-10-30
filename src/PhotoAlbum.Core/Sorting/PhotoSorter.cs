using PhotoAlbum.Core.Scanning;

namespace PhotoAlbum.Core.Sorting;

/// <summary>Deterministic photograph ordering.</summary>
public static class PhotoSorter
{
    /// <summary>
    /// Chronological: oldest first by capture date (else file timestamp); undated last.
    /// Otherwise alphabetical by filename. Ties break on filename, then full path.
    /// </summary>
    public static IReadOnlyList<Photograph> Sort(IEnumerable<Photograph> photographs, bool chronological)
    {
        var ordered = chronological
            ? photographs
                .OrderBy(p => p.SortDate is null)
                .ThenBy(p => p.SortDate)
                .ThenBy(p => p.FileName, StringComparer.OrdinalIgnoreCase)
            : photographs
                .OrderBy(p => p.FileName, StringComparer.OrdinalIgnoreCase);

        return ordered.ThenBy(p => p.FilePath, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
