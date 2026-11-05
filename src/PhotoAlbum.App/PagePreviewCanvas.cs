using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoAlbum.Core.Layout;
using PhotoAlbum.Core.Scanning;
using Rect = PhotoAlbum.Core.Layout.Rect;

namespace PhotoAlbum.App;

/// <summary>
/// Draws one album page at reduced size. Photographs are decoded as small thumbnails (never at full resolution)
/// and placed with the same <see cref="OrientationTransform"/> the PDF exporter uses, so the preview matches the PDF.
/// </summary>
internal sealed class PagePreviewCanvas : Canvas
{
    private const int MaxDecodeWidth = 800;

    private int _version;

    public PagePreviewCanvas()
    {
        Background = Brushes.White;
        ClipToBounds = true;
    }

    public void Clear()
    {
        _version++;
        Children.Clear();
        Width = 0;
        Height = 0;
    }

    /// <summary>Shows <paramref name="page"/> scaled to fit within the given size. A newer call supersedes this one.</summary>
    public async Task ShowAsync(AlbumPage page, PageSpec spec, double maxWidth, double maxHeight)
    {
        var version = ++_version;

        var scale = Math.Min(maxWidth / spec.Width, maxHeight / spec.Height);
        Width = spec.Width * scale;
        Height = spec.Height * scale;
        Children.Clear();

        var photographs = page.Photographs;
        var thumbnails = await Task.Run(() => photographs.Select(LoadThumbnail).ToArray());
        if (version != _version)
        {
            return;
        }

        for (var i = 0; i < photographs.Count; i++)
        {
            var rect = page.Layout.Photos[i];
            var target = new Rect(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);
            Children.Add(thumbnails[i] is { } thumbnail
                ? CreateImage(photographs[i], thumbnail, target)
                : CreatePlaceholder(photographs[i], target));
        }
    }

    private static UIElement CreateImage(Photograph photograph, BitmapSource thumbnail, Rect target)
    {
        // The image is laid out as a 1 x 1 square and the transform places, rotates and flips it.
        var m = OrientationTransform.ForOrientation(photograph.Orientation, target);
        var image = new Image
        {
            Source = thumbnail,
            Width = 1,
            Height = 1,
            Stretch = Stretch.Fill,
            RenderTransform = new MatrixTransform(m.M11, m.M12, m.M21, m.M22, m.OffsetX, m.OffsetY),
            ToolTip = photograph.FileName,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }

    private static UIElement CreatePlaceholder(Photograph photograph, Rect target)
    {
        var placeholder = new Border
        {
            Width = target.Width,
            Height = target.Height,
            Background = Brushes.LightGray,
            ToolTip = photograph.FileName,
            Child = new TextBlock
            {
                Text = $"Could not load {photograph.FileName}",
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(4),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            },
        };
        SetLeft(placeholder, target.X);
        SetTop(placeholder, target.Y);
        return placeholder;
    }

    /// <summary>Decodes a reduced-size copy of the photograph, or returns null if it cannot be read.</summary>
    private static BitmapSource? LoadThumbnail(Photograph photograph)
    {
        try
        {
            using var stream = File.OpenRead(photograph.FilePath);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.DecodePixelWidth = Math.Min(photograph.Width, MaxDecodeWidth);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or InvalidOperationException or FormatException)
        {
            AppLog.Error($"Preview could not load {photograph.FileName}", ex);
            return null;
        }
    }
}
