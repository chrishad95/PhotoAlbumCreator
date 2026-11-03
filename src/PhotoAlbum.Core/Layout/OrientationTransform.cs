namespace PhotoAlbum.Core.Layout;

/// <summary>
/// A 2D affine transform in the row-vector form used by PDF and PDFsharp:
/// x' = x * M11 + y * M21 + OffsetX, y' = x * M12 + y * M22 + OffsetY.
/// </summary>
public readonly record struct Affine(double M11, double M12, double M21, double M22, double OffsetX, double OffsetY)
{
    public (double X, double Y) Transform(double x, double y) =>
        ((x * M11) + (y * M21) + OffsetX, (x * M12) + (y * M22) + OffsetY);
}

/// <summary>
/// Maps a photograph as stored (before EXIF orientation) onto its display rectangle, so a renderer can
/// rotate or flip the stored image without re-encoding it.
/// </summary>
public static class OrientationTransform
{
    /// <summary>
    /// Returns the transform that maps the unit square of the stored image, with (0,0) its top-left pixel corner and
    /// (1,1) the bottom-right, onto <paramref name="target"/> as it should appear after applying the EXIF orientation.
    /// Unknown orientation values are treated as 1 (no change).
    /// </summary>
    public static Affine ForOrientation(int orientation, Rect target)
    {
        // Display position (x', y') in the unit square, as a function of the stored position (u, v):
        //   x' = P*u + Q*v + R      y' = S*u + T*v + W
        var (p, q, r, s, t, w) = orientation switch
        {
            2 => (-1, 0, 1, 0, 1, 0),   // mirror horizontally
            3 => (-1, 0, 1, 0, -1, 1),  // rotate 180
            4 => (1, 0, 0, 0, -1, 1),   // mirror vertically
            5 => (0, 1, 0, 1, 0, 0),    // transpose
            6 => (0, -1, 1, 1, 0, 0),   // rotate 90 clockwise
            7 => (0, -1, 1, -1, 0, 1),  // transverse
            8 => (0, 1, 0, -1, 0, 1),   // rotate 90 counter-clockwise
            _ => (1, 0, 0, 0, 1, 0),
        };

        return new Affine(
            M11: target.Width * p,
            M12: target.Height * s,
            M21: target.Width * q,
            M22: target.Height * t,
            OffsetX: target.X + (target.Width * r),
            OffsetY: target.Y + (target.Height * w));
    }
}
