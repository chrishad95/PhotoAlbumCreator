namespace PhotoAlbum.Core.Layout;

/// <summary>Measurement units a user can choose for page dimensions, margin and spacing.</summary>
public enum LengthUnit
{
    Inches,
    Millimeters,
    Centimeters,
}

/// <summary>Converts user-facing lengths to PDF points (1/72 inch), the unit used by the layout engine.</summary>
public static class LengthUnits
{
    public const double PointsPerInch = 72;

    public static double ToPoints(double value, LengthUnit unit) => unit switch
    {
        LengthUnit.Inches => value * PointsPerInch,
        LengthUnit.Millimeters => value / 25.4 * PointsPerInch,
        LengthUnit.Centimeters => value / 2.54 * PointsPerInch,
        _ => throw new ArgumentOutOfRangeException(nameof(unit)),
    };
}

/// <summary>A rectangle in points. The origin is the top-left corner of the page; Y grows downwards.</summary>
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double Area => Width * Height;
}

/// <summary>Page size, margin and photograph spacing, all in points.</summary>
public readonly record struct PageSpec(double Width, double Height, double Margin, double Spacing)
{
    public double UsableWidth => Width - (2 * Margin);

    public double UsableHeight => Height - (2 * Margin);

    /// <summary>Creates a spec from values entered in <paramref name="unit"/>. Width and height are never swapped.</summary>
    public static PageSpec From(double width, double height, double margin, double spacing, LengthUnit unit) =>
        new(
            LengthUnits.ToPoints(width, unit),
            LengthUnits.ToPoints(height, unit),
            LengthUnits.ToPoints(margin, unit),
            LengthUnits.ToPoints(spacing, unit));
}

/// <summary>One arrangement of a page: a rectangle per photograph, in the same order as the photographs.</summary>
public sealed record PageLayout(string TemplateName, IReadOnlyList<Rect> Photos)
{
    /// <summary>Total area covered by photographs. Larger means bigger photographs on the page.</summary>
    public double PhotoArea => Photos.Sum(p => p.Area);
}
