namespace PhotoAlbum.Core.Layout;

/// <summary>Checks a page layout against the spec's rules before it is rendered.</summary>
public static class LayoutValidator
{
    // Absolute tolerance in points for position checks; relative tolerance for aspect ratios.
    private const double PositionTolerance = 1e-4;
    private const double AspectTolerance = 1e-6;

    /// <summary>
    /// Returns a description of each problem found; an empty list means the layout is valid:
    /// one rectangle per photograph, each inside the usable area, aspect ratios preserved, no overlaps.
    /// </summary>
    public static IReadOnlyList<string> Validate(PageLayout layout, IReadOnlyList<double> aspectRatios, PageSpec page)
    {
        var problems = new List<string>();

        if (layout.Photos.Count != aspectRatios.Count)
        {
            problems.Add($"Expected {aspectRatios.Count} placements but found {layout.Photos.Count}.");
            return problems;
        }

        var left = page.Margin;
        var top = page.Margin;
        var right = page.Width - page.Margin;
        var bottom = page.Height - page.Margin;

        for (var i = 0; i < layout.Photos.Count; i++)
        {
            var r = layout.Photos[i];
            var label = $"Photograph {i + 1}";

            if (!double.IsFinite(r.X) || !double.IsFinite(r.Y) || !double.IsFinite(r.Width) || !double.IsFinite(r.Height) ||
                r.Width <= 0 || r.Height <= 0)
            {
                problems.Add($"{label} has an invalid size or position.");
                continue;
            }

            if (r.X < left - PositionTolerance || r.Y < top - PositionTolerance ||
                r.Right > right + PositionTolerance || r.Bottom > bottom + PositionTolerance)
            {
                problems.Add($"{label} extends beyond the usable page area.");
            }

            var expected = aspectRatios[i];
            if (Math.Abs((r.Width / r.Height) - expected) > expected * AspectTolerance)
            {
                problems.Add($"{label} does not keep its aspect ratio.");
            }
        }

        for (var i = 0; i < layout.Photos.Count; i++)
        {
            for (var j = i + 1; j < layout.Photos.Count; j++)
            {
                var a = layout.Photos[i];
                var b = layout.Photos[j];
                if (a.X < b.Right - PositionTolerance && b.X < a.Right - PositionTolerance &&
                    a.Y < b.Bottom - PositionTolerance && b.Y < a.Bottom - PositionTolerance)
                {
                    problems.Add($"Photographs {i + 1} and {j + 1} overlap.");
                }
            }
        }

        return problems;
    }
}
