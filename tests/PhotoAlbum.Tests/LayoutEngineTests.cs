using PhotoAlbum.Core.Layout;

namespace PhotoAlbum.Tests;

public class LayoutEngineTests
{
    // 8 x 10 inches in points, with the spec's default margin (0.25") and spacing (0.10").
    private static readonly PageSpec Portrait = PageSpec.From(8, 10, 0.25, 0.10, LengthUnit.Inches);
    private static readonly PageSpec Landscape = PageSpec.From(10, 8, 0.25, 0.10, LengthUnit.Inches);

    private static double[] Ratios(int count, params double[] pattern) =>
        Enumerable.Range(0, count).Select(i => pattern[i % pattern.Length]).ToArray();

    // ---- Units ----

    [Theory]
    [InlineData(1, LengthUnit.Inches, 72)]
    [InlineData(25.4, LengthUnit.Millimeters, 72)]
    [InlineData(2.54, LengthUnit.Centimeters, 72)]
    [InlineData(210, LengthUnit.Millimeters, 595.2755905511812)]
    public void ToPoints_ConvertsUnits(double value, LengthUnit unit, double expected)
    {
        Assert.Equal(expected, LengthUnits.ToPoints(value, unit), 6);
    }

    [Fact]
    public void PageSpec_DoesNotSwapWidthAndHeight()
    {
        Assert.True(Portrait.Height > Portrait.Width);
        Assert.True(Landscape.Width > Landscape.Height);
    }

    // ---- Templates ----

    [Fact]
    public void BuiltInTemplates_LoadAndCoverOneToEightPhotos()
    {
        var templates = LayoutTemplates.BuiltIn;

        Assert.Equal(13, templates.Count);
        for (var n = 1; n <= 8; n++)
        {
            Assert.Contains(templates, t => t.PhotoCount == n);
        }

        Assert.Equal(templates.Count, templates.Select(t => t.Name).Distinct().Count());
    }

    [Theory]
    [InlineData(15, new[] { 5, 5, 5 })]
    [InlineData(16, new[] { 4, 4, 4, 4 })]
    [InlineData(17, new[] { 4, 4, 4, 5 })]
    [InlineData(9, new[] { 3, 3, 3 })]
    [InlineData(10, new[] { 3, 3, 4 })]
    public void Grid_HasEqualRowsWithLongerRowsLast(int count, int[] expectedRowSizes)
    {
        var grid = LayoutTemplates.CreateGrid(count);

        var rows = Assert.IsType<SplitNode>(grid.Root);
        var sizes = rows.Children.Select(r => ((SplitNode)r).Children.Count).ToArray();

        Assert.Equal(expectedRowSizes, sizes);
        Assert.Equal(count, grid.PhotoCount);
    }

    [Fact]
    public void ForPhotoCount_AboveEight_ReturnsGeneratedGrid()
    {
        var templates = LayoutTemplates.ForPhotoCount(12);

        var single = Assert.Single(templates);
        Assert.Equal(12, single.PhotoCount);
    }

    [Fact]
    public void Parse_RejectsMissingOrDuplicateSlots()
    {
        const string duplicate = """
            { "name": "bad", "photoCount": 2,
              "root": { "split": "columns", "children": [ { "slot": 1 }, { "slot": 1 } ] } }
            """;

        Assert.Throws<InvalidDataException>(() => LayoutTemplates.Parse(duplicate));
        Assert.Throws<InvalidDataException>(() => LayoutTemplates.Parse("not json"));
        Assert.Throws<InvalidDataException>(() => LayoutTemplates.Parse("""{ "name": "x", "photoCount": 1 }"""));
    }

    // ---- Geometry: every template must produce a valid layout for varied photographs ----

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(30)]
    public void EveryPhotoCount_ProducesValidCandidates_OnPortraitAndLandscapePages(int count)
    {
        var ratios = Ratios(count, 1.5, 0.667, 1.333, 1.0, 0.75, 1.778);

        foreach (var page in new[] { Portrait, Landscape })
        {
            var candidates = LayoutEngine.CreateCandidates(ratios, page);

            Assert.NotEmpty(candidates);
            Assert.All(candidates, c =>
            {
                Assert.Empty(LayoutValidator.Validate(c, ratios, page));
                Assert.Equal(count, c.Photos.Count);
            });
        }
    }

    [Fact]
    public void EveryTemplate_IsValidForManyRandomPhotoShapes()
    {
        var random = new Random(7);
        foreach (var template in LayoutTemplates.BuiltIn)
        {
            for (var trial = 0; trial < 50; trial++)
            {
                var ratios = Enumerable.Range(0, template.PhotoCount)
                    .Select(_ => Math.Exp((random.NextDouble() * 2 - 1) * Math.Log(3))) // 1/3 .. 3
                    .ToArray();
                var page = trial % 2 == 0 ? Portrait : Landscape;

                var layout = LayoutEngine.TryPlace(template, ratios, page);

                Assert.NotNull(layout);
                Assert.Empty(LayoutValidator.Validate(layout, ratios, page));
            }
        }
    }

    [Fact]
    public void SinglePhoto_FillsUsableAreaAlongTheLimitingSide_AndIsCentered()
    {
        var page = PageSpec.From(8, 10, 0.25, 0.10, LengthUnit.Inches);
        var layout = LayoutEngine.CreateCandidates([1.5], page).Single().Photos.Single();

        // A 3:2 landscape photo is limited by usable width (7.5") and sits centered vertically.
        Assert.Equal(page.UsableWidth, layout.Width, 6);
        Assert.Equal(page.UsableWidth / 1.5, layout.Height, 6);
        Assert.Equal(page.Margin, layout.X, 6);
        Assert.Equal((page.Height - layout.Height) / 2, layout.Y, 6);
    }

    [Fact]
    public void SideBySide_UsesSpacingBetweenPhotos_AndSharesHeight()
    {
        var template = LayoutTemplates.BuiltIn.Single(t => t.Name == "2-photos-side-by-side");
        var layout = LayoutEngine.TryPlace(template, [1.0, 1.0], Landscape)!;

        var (left, right) = (layout.Photos[0], layout.Photos[1]);
        Assert.Equal(Landscape.Spacing, right.X - left.Right, 6);
        Assert.Equal(left.Height, right.Height, 6);
        Assert.Equal(left.Width, right.Width, 6);
    }

    [Fact]
    public void ZeroMarginAndSpacing_StillValid_AndTouchesPageEdges()
    {
        var page = new PageSpec(600, 800, 0, 0);
        var ratios = new[] { 1.0, 1.0, 1.0, 1.0 };

        var layout = LayoutEngine.CreateCandidates(ratios, page).First();

        Assert.Empty(LayoutValidator.Validate(layout, ratios, page));
        Assert.True(layout.Photos.Min(p => p.X) < 1e-6 || layout.Photos.Min(p => p.Y) < 1e-6);
    }

    [Fact]
    public void Candidates_AreRankedByPhotoAreaThenName_AndAreRepeatable()
    {
        var ratios = new[] { 1.5, 0.667, 1.5, 1.0, 1.333 };

        var first = LayoutEngine.CreateCandidates(ratios, Portrait);
        var second = LayoutEngine.CreateCandidates(ratios, Portrait);

        Assert.True(first.Count >= 2);
        Assert.Equal(first.Select(c => c.TemplateName), second.Select(c => c.TemplateName));
        for (var i = 1; i < first.Count; i++)
        {
            Assert.True(first[i - 1].PhotoArea >= first[i].PhotoArea - 0.01);
        }
    }

    [Fact]
    public void SpacingTooLargeForPage_ProducesNoCandidates()
    {
        // The only 8-photo template is 4 across: 3 gaps of 200pt = 600pt, more than the 580pt usable width.
        var page = new PageSpec(600, 800, 10, 200);

        var candidates = LayoutEngine.CreateCandidates(Ratios(8, 1.0), page);

        Assert.Empty(candidates);
    }

    [Fact]
    public void InvalidPageOrAspectRatio_Throws()
    {
        Assert.Throws<ArgumentException>(() => LayoutEngine.CreateCandidates([1.0], new PageSpec(100, 100, 60, 0)));
        Assert.Throws<ArgumentException>(() => LayoutEngine.CreateCandidates([0.0], Portrait));
        Assert.Throws<ArgumentException>(() => LayoutEngine.CreateCandidates([double.NaN], Portrait));
    }

    // ---- Validator catches broken layouts ----

    [Fact]
    public void Validator_DetectsOverlapOutOfBoundsAndStretching()
    {
        var page = new PageSpec(600, 800, 20, 10);

        var overlapping = new PageLayout("x", [new Rect(20, 20, 100, 100), new Rect(60, 60, 100, 100)]);
        var outside = new PageLayout("x", [new Rect(0, 0, 100, 100)]);
        var stretched = new PageLayout("x", [new Rect(20, 20, 200, 100)]);

        Assert.Contains(LayoutValidator.Validate(overlapping, [1.0, 1.0], page), p => p.Contains("overlap"));
        Assert.Contains(LayoutValidator.Validate(outside, [1.0], page), p => p.Contains("beyond"));
        Assert.Contains(LayoutValidator.Validate(stretched, [1.0], page), p => p.Contains("aspect"));
    }

    [Fact]
    public void Validator_AllowsTouchingEdges()
    {
        var page = new PageSpec(600, 800, 20, 0);
        var touching = new PageLayout("x", [new Rect(20, 20, 100, 100), new Rect(120, 20, 100, 100)]);

        Assert.Empty(LayoutValidator.Validate(touching, [1.0, 1.0], page));
    }
}
