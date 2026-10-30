using PhotoAlbum.Core.Distribution;
using PhotoAlbum.Core.Scanning;

namespace PhotoAlbum.Tests;

public class PageDistributorTests
{
    [Fact]
    public void TwelvePhotos_Min2_Max4_IsValid()
    {
        var result = PageDistributor.Calculate(12, 2, 4);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.PageSizes.Sum());
        Assert.All(result.PageSizes, s => Assert.InRange(s, 2, 4));
    }

    [Fact]
    public void FivePhotos_Min3_Max4_IsImpossible()
    {
        // One page holds at most 4; two pages need at least 6.
        var result = PageDistributor.Calculate(5, 3, 4);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.PageSizes);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void ElevenPhotos_Min3_Max4_IsPossible()
    {
        // The spec's example claims this is impossible, but 3 + 4 + 4 = 11.
        var result = PageDistributor.Calculate(11, 3, 4);

        Assert.True(result.IsSuccess);
        Assert.Equal([3, 4, 4], result.PageSizes);
    }

    [Fact]
    public void ZeroPhotos_ProducesNoPages()
    {
        var result = PageDistributor.Calculate(0, 2, 4);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.PageSizes);
    }

    [Theory]
    [InlineData(-1, 2, 4)]
    [InlineData(5, 0, 4)]
    [InlineData(5, 4, 2)]
    public void InvalidInputs_Fail(int count, int min, int max)
    {
        Assert.False(PageDistributor.Calculate(count, min, max).IsSuccess);
    }

    [Fact]
    public void FewerPhotosThanMinimum_Fails()
    {
        Assert.False(PageDistributor.Calculate(1, 2, 4).IsSuccess);
    }

    [Fact]
    public void SinglePhotoWithMinimumOne_IsOnePage()
    {
        var result = PageDistributor.Calculate(1, 1, 4);

        Assert.True(result.IsSuccess);
        Assert.Equal([1], result.PageSizes);
    }

    [Fact]
    public void PrefersBalancedPagesOverFillingToMaximum()
    {
        // 12 photographs in 2-5 per page: three pages of four, not 5+5+2.
        var result = PageDistributor.Calculate(12, 2, 5);

        Assert.Equal([4, 4, 4], result.PageSizes);
    }

    [Fact]
    public void UnevenSplit_LeavesLargestPagesLast()
    {
        var result = PageDistributor.Calculate(10, 3, 4);

        Assert.Equal([3, 3, 4], result.PageSizes);
    }

    [Fact]
    public void ExhaustiveSmallRange_MatchesConstraintsAndFeasibility()
    {
        for (var min = 1; min <= 6; min++)
        {
            for (var max = min; max <= 8; max++)
            {
                for (var count = 0; count <= 60; count++)
                {
                    var result = PageDistributor.Calculate(count, min, max);
                    var feasible = Enumerable.Range(0, count + 1)
                        .Any(pages => pages * min <= count && count <= pages * max && (pages > 0 || count == 0));

                    Assert.Equal(feasible, result.IsSuccess);

                    if (!result.IsSuccess)
                    {
                        continue;
                    }

                    Assert.Equal(count, result.PageSizes.Sum());
                    Assert.All(result.PageSizes, s => Assert.InRange(s, min, max));
                    if (result.PageSizes.Count > 0)
                    {
                        Assert.True(result.PageSizes.Max() - result.PageSizes.Min() <= 1);
                    }
                }
            }
        }
    }

    [Fact]
    public void Split_PreservesOrderAndLosesNothing()
    {
        var items = Enumerable.Range(1, 10).ToList();

        var pages = PageDistributor.Split(items, [3, 3, 4]);

        Assert.Equal(3, pages.Count);
        Assert.Equal(items, pages.SelectMany(p => p).ToList());
        Assert.Equal([1, 2, 3], pages[0]);
        Assert.Equal([7, 8, 9, 10], pages[2]);
    }

    [Fact]
    public void Split_WithMismatchedSizes_Throws()
    {
        Assert.Throws<ArgumentException>(() => PageDistributor.Split([1, 2, 3], [2, 2]));
    }

    // Three photographs seconds apart, then a ten-day gap, then five photographs seconds apart.
    private static List<Photograph> ThreeThenFive()
    {
        var start = new DateTime(2024, 6, 1, 10, 0, 0);
        var times = new[] { 0, 10, 20 }.Select(s => start.AddSeconds(s))
            .Concat(new[] { 0, 10, 20, 30, 40 }.Select(s => start.AddDays(10).AddSeconds(s)));
        return times.Select((t, i) =>
            new Photograph($@"C:\p\{i}.jpg", 100, 100, 1, t, null, DateSource.ExifDateTimeOriginal)).ToList();
    }

    [Fact]
    public void Chronological_BreaksAtTheBigTimeGap()
    {
        var gaps = PageDistributor.GapsBetween(ThreeThenFive());

        var withGaps = PageDistributor.Calculate(8, 2, 6, gaps);
        var withoutGaps = PageDistributor.Calculate(8, 2, 6);

        Assert.Equal([3, 5], withGaps.PageSizes);
        Assert.Equal([4, 4], withoutGaps.PageSizes);
    }

    [Fact]
    public void Chronological_StillRespectsMinimumAndMaximum()
    {
        // The natural break is after 3 photographs, but the maximum is 4 and the minimum 4: must be 4 + 4.
        var gaps = PageDistributor.GapsBetween(ThreeThenFive());

        var result = PageDistributor.Calculate(8, 4, 4, gaps);

        Assert.Equal([4, 4], result.PageSizes);
    }

    [Fact]
    public void Chronological_EvenlySpacedPhotos_StayBalanced()
    {
        var gaps = Enumerable.Repeat(TimeSpan.FromMinutes(1), 9).ToList();

        var result = PageDistributor.Calculate(10, 3, 4, gaps);

        Assert.Equal(10, result.PageSizes.Sum());
        Assert.All(result.PageSizes, s => Assert.InRange(s, 3, 4));
    }

    [Fact]
    public void Chronological_NoUsableDates_MatchesEvenDistribution()
    {
        var gaps = Enumerable.Repeat(TimeSpan.Zero, 9).ToList();

        Assert.Equal(
            PageDistributor.Calculate(10, 3, 4).PageSizes,
            PageDistributor.Calculate(10, 3, 4, gaps).PageSizes);
    }

    [Fact]
    public void Chronological_RandomGaps_AlwaysSatisfyConstraints()
    {
        var random = new Random(42);
        for (var trial = 0; trial < 200; trial++)
        {
            var count = random.Next(1, 80);
            var min = random.Next(1, 4);
            var max = min + random.Next(0, 5);
            var gaps = Enumerable.Range(0, Math.Max(0, count - 1))
                .Select(_ => TimeSpan.FromSeconds(Math.Pow(10, random.NextDouble() * 7))).ToList();

            var withGaps = PageDistributor.Calculate(count, min, max, gaps);
            var withoutGaps = PageDistributor.Calculate(count, min, max);

            Assert.Equal(withoutGaps.IsSuccess, withGaps.IsSuccess);
            if (!withGaps.IsSuccess)
            {
                continue;
            }

            Assert.Equal(count, withGaps.PageSizes.Sum());
            Assert.Equal(withoutGaps.PageSizes.Count, withGaps.PageSizes.Count);
            Assert.All(withGaps.PageSizes, s => Assert.InRange(s, min, max));
        }
    }

    [Fact]
    public void WrongNumberOfGaps_Throws()
    {
        Assert.Throws<ArgumentException>(() => PageDistributor.Calculate(5, 1, 4, [TimeSpan.FromSeconds(1)]));
    }

    [Fact]
    public void GapsBetween_UndatedPhotosGetZeroGap()
    {
        var dated = new Photograph(@"C:\p\a.jpg", 1, 1, 1, new DateTime(2024, 1, 1), null, DateSource.ExifDateTimeOriginal);
        var undated = new Photograph(@"C:\p\b.jpg", 1, 1, 1, null, null, DateSource.None);
        var later = new Photograph(@"C:\p\c.jpg", 1, 1, 1, new DateTime(2024, 1, 2), null, DateSource.ExifDateTimeOriginal);

        var gaps = PageDistributor.GapsBetween([dated, later, undated]);

        Assert.Equal([TimeSpan.FromDays(1), TimeSpan.Zero], gaps);
    }
}
