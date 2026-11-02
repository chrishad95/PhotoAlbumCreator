namespace PhotoAlbum.Core.Layout;

/// <summary>
/// Places photographs on a page using layout templates. Photographs are never cropped or stretched.
/// Pure geometry: no UI, image or PDF dependencies. All lengths are in points.
/// </summary>
/// <remarks>
/// Every node's width is a linear function of its height: width = A * height + B. Slots are (aspect ratio, 0);
/// columns add their children's terms plus spacing; rows combine them through the inverse relationship.
/// That lets any nesting of rows and columns, with spacing, be fitted exactly into the usable page area.
/// </remarks>
public static class LayoutEngine
{
    private readonly record struct Extent(double A, double B);

    /// <summary>
    /// Candidate layouts for one page, best first (largest total photograph area, ties broken by template name).
    /// A Shuffle control can step through this list. Candidates that fail validation are dropped.
    /// </summary>
    /// <param name="aspectRatios">Width / height of each photograph on the page, in page order.</param>
    public static IReadOnlyList<PageLayout> CreateCandidates(IReadOnlyList<double> aspectRatios, PageSpec page) =>
        CreateCandidates(aspectRatios, page, LayoutTemplates.ForPhotoCount(aspectRatios.Count));

    public static IReadOnlyList<PageLayout> CreateCandidates(
        IReadOnlyList<double> aspectRatios, PageSpec page, IEnumerable<LayoutTemplate> templates)
    {
        CheckInputs(aspectRatios, page);

        return templates
            .Select(template => TryPlace(template, aspectRatios, page))
            .OfType<PageLayout>()
            .Where(layout => LayoutValidator.Validate(layout, aspectRatios, page).Count == 0)
            .OrderByDescending(layout => Math.Round(layout.PhotoArea, 3))
            .ThenBy(layout => layout.TemplateName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Fits one template to the page: as large as possible, centered in the usable area.
    /// Returns null when the template does not match the photograph count or cannot fit (for example, spacing too large).
    /// </summary>
    public static PageLayout? TryPlace(LayoutTemplate template, IReadOnlyList<double> aspectRatios, PageSpec page)
    {
        if (template.PhotoCount != aspectRatios.Count)
        {
            return null;
        }

        var extent = Measure(template.Root, aspectRatios, page.Spacing);
        var usableWidth = page.UsableWidth;
        var usableHeight = page.UsableHeight;

        var height = usableHeight;
        var width = (extent.A * height) + extent.B;
        if (width > usableWidth)
        {
            width = usableWidth;
            height = (width - extent.B) / extent.A;
        }

        if (!(width > 0 && height > 0))
        {
            return null;
        }

        var x = page.Margin + ((usableWidth - width) / 2);
        var y = page.Margin + ((usableHeight - height) / 2);

        var rects = new Rect[aspectRatios.Count];
        Place(template.Root, x, y, width, height, aspectRatios, page.Spacing, rects);
        return new PageLayout(template.Name, rects);
    }

    private static void CheckInputs(IReadOnlyList<double> aspectRatios, PageSpec page)
    {
        if (!(page.Width > 0 && page.Height > 0 && page.Margin >= 0 && page.Spacing >= 0 &&
              page.UsableWidth > 0 && page.UsableHeight > 0))
        {
            throw new ArgumentException("Page size, margin and spacing must leave a positive usable area.", nameof(page));
        }

        if (aspectRatios.Any(r => !(r > 0) || !double.IsFinite(r)))
        {
            throw new ArgumentException("Every aspect ratio must be a positive, finite number.", nameof(aspectRatios));
        }
    }

    private static Extent Measure(LayoutNode node, IReadOnlyList<double> aspectRatios, double spacing)
    {
        switch (node)
        {
            case SlotNode slot:
                return new Extent(aspectRatios[slot.Slot - 1], 0);

            case SplitNode { Direction: SplitDirection.Columns } columns:
            {
                double a = 0;
                var b = (columns.Children.Count - 1) * spacing;
                foreach (var child in columns.Children)
                {
                    var e = Measure(child, aspectRatios, spacing);
                    a += e.A;
                    b += e.B;
                }

                return new Extent(a, b);
            }

            case SplitNode rows:
            {
                double inverseSum = 0;
                double offsetSum = 0;
                foreach (var child in rows.Children)
                {
                    var e = Measure(child, aspectRatios, spacing);
                    inverseSum += 1 / e.A;
                    offsetSum += e.B / e.A;
                }

                var a = 1 / inverseSum;
                return new Extent(a, (offsetSum - ((rows.Children.Count - 1) * spacing)) * a);
            }

            default:
                throw new ArgumentException("Unknown layout node.", nameof(node));
        }
    }

    private static void Place(
        LayoutNode node, double x, double y, double width, double height,
        IReadOnlyList<double> aspectRatios, double spacing, Rect[] output)
    {
        switch (node)
        {
            case SlotNode slot:
                output[slot.Slot - 1] = new Rect(x, y, width, height);
                break;

            case SplitNode { Direction: SplitDirection.Columns } columns:
                foreach (var child in columns.Children)
                {
                    var e = Measure(child, aspectRatios, spacing);
                    var childWidth = (e.A * height) + e.B;
                    Place(child, x, y, childWidth, height, aspectRatios, spacing, output);
                    x += childWidth + spacing;
                }

                break;

            case SplitNode rows:
                foreach (var child in rows.Children)
                {
                    var e = Measure(child, aspectRatios, spacing);
                    var childHeight = (width - e.B) / e.A;
                    Place(child, x, y, width, childHeight, aspectRatios, spacing, output);
                    y += childHeight + spacing;
                }

                break;
        }
    }
}
