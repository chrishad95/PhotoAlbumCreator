using PhotoAlbum.Core.Scanning;

namespace PhotoAlbum.Core.Distribution;

/// <summary>Result of planning how many photographs go on each page.</summary>
public sealed record DistributionResult(bool IsSuccess, IReadOnlyList<int> PageSizes, string? Error)
{
    public static DistributionResult Success(IReadOnlyList<int> sizes) => new(true, sizes, null);

    public static DistributionResult Failure(string error) => new(false, [], error);
}

/// <summary>Plans page sizes within a minimum/maximum photographs-per-page range. Pure logic, no I/O.</summary>
public static class PageDistributor
{
    // How strongly uneven pages are discouraged compared with breaking at a time gap.
    // A page one photo off the ideal size costs 0.1; the biggest gap in the set is worth 1.0.
    private const double UnevenPagePenalty = 0.1;

    /// <summary>
    /// A distribution exists exactly when ceil(n / max) &lt;= floor(n / min). Among feasible page counts,
    /// picks the one whose average page size is closest to the middle of the range (ties go to fewer pages),
    /// then spreads photographs as evenly as possible: page sizes differ by at most one.
    /// When <paramref name="gaps"/> is supplied (chronological order), page breaks are shifted towards the
    /// largest time gaps so photographs from the same time frame tend to share a page. The minimum and
    /// maximum are always respected; the page count is unchanged.
    /// </summary>
    /// <param name="gaps">Time between each consecutive pair of ordered photographs (length = photoCount - 1), or null.</param>
    public static DistributionResult Calculate(
        int photoCount, int minPerPage, int maxPerPage, IReadOnlyList<TimeSpan>? gaps = null)
    {
        if (photoCount < 0)
        {
            return DistributionResult.Failure("The photograph count cannot be negative.");
        }

        if (minPerPage < 1)
        {
            return DistributionResult.Failure("The minimum photographs per page must be at least 1.");
        }

        if (maxPerPage < minPerPage)
        {
            return DistributionResult.Failure("The maximum photographs per page cannot be less than the minimum.");
        }

        if (gaps is not null && gaps.Count != Math.Max(0, photoCount - 1))
        {
            throw new ArgumentException("There must be exactly one gap between each pair of consecutive photographs.", nameof(gaps));
        }

        if (photoCount == 0)
        {
            return DistributionResult.Success([]);
        }

        var fewestPages = (photoCount + maxPerPage - 1) / maxPerPage;
        var mostPages = photoCount / minPerPage;

        if (fewestPages > mostPages)
        {
            return DistributionResult.Failure(
                $"{photoCount} photographs cannot be split into pages of {minPerPage} to {maxPerPage} photographs each. " +
                "Adjust the minimum or maximum photographs per page.");
        }

        var target = (minPerPage + maxPerPage) / 2.0;
        var pages = fewestPages;
        var bestDistance = Math.Abs((double)photoCount / fewestPages - target);

        for (var candidate = fewestPages + 1; candidate <= mostPages; candidate++)
        {
            var distance = Math.Abs((double)photoCount / candidate - target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                pages = candidate;
            }
        }

        var baseSize = photoCount / pages;
        var larger = photoCount % pages;
        var sizes = new int[pages];
        for (var i = 0; i < pages; i++)
        {
            sizes[i] = i >= pages - larger ? baseSize + 1 : baseSize;
        }

        if (gaps is not null && pages > 1)
        {
            sizes = GroupByTime(photoCount, pages, minPerPage, maxPerPage, gaps) ?? sizes;
        }

        return DistributionResult.Success(sizes);
    }

    /// <summary>
    /// Time between each consecutive pair of chronologically ordered photographs. A pair where either
    /// photograph has no date gets a zero gap, which expresses no preference about breaking there.
    /// </summary>
    public static IReadOnlyList<TimeSpan> GapsBetween(IReadOnlyList<Photograph> ordered)
    {
        var gaps = new TimeSpan[Math.Max(0, ordered.Count - 1)];
        for (var i = 0; i < gaps.Length; i++)
        {
            if (ordered[i].SortDate is { } earlier && ordered[i + 1].SortDate is { } later && later > earlier)
            {
                gaps[i] = later - earlier;
            }
        }

        return gaps;
    }

    /// <summary>Splits an ordered list into consecutive pages of the given sizes. Order is preserved.</summary>
    public static IReadOnlyList<IReadOnlyList<T>> Split<T>(IReadOnlyList<T> items, IReadOnlyList<int> pageSizes)
    {
        if (pageSizes.Sum() != items.Count)
        {
            throw new ArgumentException("Page sizes must add up to the number of items.", nameof(pageSizes));
        }

        var pages = new List<IReadOnlyList<T>>(pageSizes.Count);
        var offset = 0;
        foreach (var size in pageSizes)
        {
            pages.Add(items.Skip(offset).Take(size).ToList());
            offset += size;
        }

        return pages;
    }

    /// <summary>
    /// Dynamic programme over (pages used, photographs placed). Each page boundary earns a reward that grows
    /// with the log of the time gap there; each page loses points for straying from the ideal size.
    /// Returns null when there is no gap information to act on.
    /// </summary>
    private static int[]? GroupByTime(int count, int pages, int min, int max, IReadOnlyList<TimeSpan> gaps)
    {
        var weights = gaps.Select(g => Math.Log(1 + Math.Max(0, g.TotalSeconds))).ToArray();
        var maxWeight = weights.Max();
        if (maxWeight <= 0)
        {
            return null;
        }

        var ideal = (double)count / pages;
        var best = new double[pages + 1, count + 1];
        var chosen = new int[pages + 1, count + 1];
        for (var j = 0; j <= pages; j++)
        {
            for (var i = 0; i <= count; i++)
            {
                best[j, i] = double.NegativeInfinity;
            }
        }

        best[0, 0] = 0;

        for (var j = 1; j <= pages; j++)
        {
            for (var i = j * min; i <= Math.Min(j * max, count); i++)
            {
                for (var size = min; size <= max && size <= i; size++)
                {
                    var previous = best[j - 1, i - size];
                    if (double.IsNegativeInfinity(previous))
                    {
                        continue;
                    }

                    var score = previous - UnevenPagePenalty * (size - ideal) * (size - ideal);
                    if (j > 1)
                    {
                        score += weights[i - size - 1] / maxWeight;
                    }

                    if (score > best[j, i])
                    {
                        best[j, i] = score;
                        chosen[j, i] = size;
                    }
                }
            }
        }

        if (double.IsNegativeInfinity(best[pages, count]))
        {
            return null;
        }

        var result = new int[pages];
        var remaining = count;
        for (var j = pages; j >= 1; j--)
        {
            result[j - 1] = chosen[j, remaining];
            remaining -= result[j - 1];
        }

        return result;
    }
}
