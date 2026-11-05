using PhotoAlbum.Core.Scanning;

namespace PhotoAlbum.Core.Layout;

/// <summary>One album page: its photographs in page order and where each one is placed.</summary>
public sealed record AlbumPage(IReadOnlyList<Photograph> Photographs, PageLayout Layout);
