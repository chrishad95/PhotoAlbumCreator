using PhotoAlbum.Core.Distribution;
using PhotoAlbum.Core.Layout;
using PhotoAlbum.Core.Scanning;
using PhotoAlbum.Core.Sorting;

namespace PhotoAlbum.Core.Album;

/// <summary>Everything the user configures for an album. Lengths are in <see cref="Unit"/>.</summary>
public sealed record AlbumSettings(
    double PageWidth,
    double PageHeight,
    LengthUnit Unit,
    int MinPerPage,
    int MaxPerPage,
    bool Chronological,
    double Margin,
    double Spacing)
{
    public PageSpec ToPageSpec() => PageSpec.From(PageWidth, PageHeight, Margin, Spacing, Unit);

    /// <summary>Returns a message for the user describing the first problem, or null when the settings are usable.</summary>
    public string? Validate()
    {
        if (!double.IsFinite(PageWidth) || !double.IsFinite(PageHeight) || PageWidth <= 0 || PageHeight <= 0)
        {
            return "Enter a page width and height greater than zero.";
        }

        if (!double.IsFinite(Margin) || !double.IsFinite(Spacing) || Margin < 0 || Spacing < 0)
        {
            return "The page margin and photo spacing cannot be negative.";
        }

        if (MinPerPage < 1)
        {
            return "The minimum photographs per page must be at least 1.";
        }

        if (MaxPerPage < MinPerPage)
        {
            return "The maximum photographs per page cannot be less than the minimum.";
        }

        var page = ToPageSpec();
        if (page.UsableWidth <= 0 || page.UsableHeight <= 0)
        {
            return "The page margin is too large for the page size.";
        }

        return null;
    }
}

/// <summary>Outcome of arranging photographs into pages.</summary>
public sealed record AlbumResult(
    bool IsSuccess,
    IReadOnlyList<AlbumPage> Pages,
    PageSpec Page,
    IReadOnlyList<string> Warnings,
    string? Error)
{
    public int PhotographCount => Pages.Sum(p => p.Photographs.Count);
}

/// <summary>Turns scanned photographs and settings into laid-out album pages. Pure logic; no UI, files or PDF.</summary>
public static class AlbumBuilder
{
    public static AlbumResult Build(
        IReadOnlyList<Photograph> photographs, AlbumSettings settings, CancellationToken cancellationToken = default)
    {
        if (photographs.Count == 0)
        {
            return Failure("There are no photographs to arrange.");
        }

        if (settings.Validate() is { } problem)
        {
            return Failure(problem);
        }

        var page = settings.ToPageSpec();
        var sorted = PhotoSorter.Sort(photographs, settings.Chronological);
        var gaps = settings.Chronological ? PageDistributor.GapsBetween(sorted) : null;

        var distribution = PageDistributor.Calculate(sorted.Count, settings.MinPerPage, settings.MaxPerPage, gaps);
        if (!distribution.IsSuccess)
        {
            return Failure(distribution.Error!);
        }

        var groups = PageDistributor.Split(sorted, distribution.PageSizes);
        var pages = new List<AlbumPage>(groups.Count);
        for (var i = 0; i < groups.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ratios = groups[i].Select(p => p.AspectRatio).ToArray();
            var layout = LayoutEngine.CreateCandidates(ratios, page).FirstOrDefault();
            if (layout is null)
            {
                return Failure($"Page {i + 1} could not be laid out. Try a smaller page margin or photo spacing.");
            }

            pages.Add(new AlbumPage(groups[i], layout));
        }

        var warnings = new List<string>();
        var undated = sorted.Count(p => p.CaptureDate is null);
        if (settings.Chronological && undated > 0)
        {
            warnings.Add(
                $"{undated} of {sorted.Count} photographs have no capture date in their metadata, " +
                "so their file dates were used for ordering.");
        }

        return new AlbumResult(true, pages, page, warnings, null);
    }

    private static AlbumResult Failure(string error) =>
        new(false, [], new PageSpec(0, 0, 0, 0), [], error);
}
