namespace PhotoAlbum.Core.Layout;

public enum SplitDirection
{
    /// <summary>Children are stacked top to bottom and share a width.</summary>
    Rows,

    /// <summary>Children sit side by side and share a height.</summary>
    Columns,
}

/// <summary>A node in a layout tree: either a photograph slot or a split into children.</summary>
public abstract record LayoutNode;

/// <summary>A photograph position. <see cref="Slot"/> is 1-based and maps to the nth photograph on the page.</summary>
public sealed record SlotNode(int Slot) : LayoutNode;

public sealed record SplitNode(SplitDirection Direction, IReadOnlyList<LayoutNode> Children) : LayoutNode;

/// <summary>A reusable arrangement for a fixed number of photographs.</summary>
public sealed record LayoutTemplate(string Name, string Description, int PhotoCount, LayoutNode Root);
