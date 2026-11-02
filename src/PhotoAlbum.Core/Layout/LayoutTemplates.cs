using System.Text.Json;

namespace PhotoAlbum.Core.Layout;

/// <summary>Loads layout templates from embedded JSON and generates grids for large photograph counts.</summary>
public static class LayoutTemplates
{
    private const string ResourcePrefix = "Templates/";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<IReadOnlyList<LayoutTemplate>> BuiltInTemplates = new(LoadBuiltIn);

    /// <summary>The templates shipped inside the assembly, in a stable order.</summary>
    public static IReadOnlyList<LayoutTemplate> BuiltIn => BuiltInTemplates.Value;

    /// <summary>
    /// Templates for a photograph count. When none exist (more than 8 photographs), returns a single generated grid.
    /// </summary>
    public static IReadOnlyList<LayoutTemplate> ForPhotoCount(int photoCount)
    {
        if (photoCount < 1)
        {
            return [];
        }

        var matching = BuiltIn.Where(t => t.PhotoCount == photoCount).ToList();
        return matching.Count > 0 ? matching : [CreateGrid(photoCount)];
    }

    /// <summary>
    /// Builds a grid with floor(sqrt(n)) rows and as equal a number of photographs per row as possible.
    /// Longer rows come last: 15 = 5+5+5, 16 = 4+4+4+4, 17 = 4+4+4+5.
    /// </summary>
    public static LayoutTemplate CreateGrid(int photoCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(photoCount, 1);

        var rows = (int)Math.Floor(Math.Sqrt(photoCount));
        var baseSize = photoCount / rows;
        var longerRows = photoCount % rows;

        var slot = 1;
        var rowNodes = new List<LayoutNode>(rows);
        for (var row = 0; row < rows; row++)
        {
            var size = row >= rows - longerRows ? baseSize + 1 : baseSize;
            var cells = new List<LayoutNode>(size);
            for (var i = 0; i < size; i++)
            {
                cells.Add(new SlotNode(slot++));
            }

            rowNodes.Add(new SplitNode(SplitDirection.Columns, cells));
        }

        return new LayoutTemplate(
            $"{photoCount}-photos-grid",
            "Generated grid with an equal number of photographs per row where possible.",
            photoCount,
            new SplitNode(SplitDirection.Rows, rowNodes));
    }

    /// <summary>Parses and validates one template. Throws <see cref="InvalidDataException"/> if it is malformed.</summary>
    public static LayoutTemplate Parse(string json)
    {
        TemplateDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<TemplateDto>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The layout template is not valid JSON.", ex);
        }

        if (dto?.Root is null || string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new InvalidDataException("A layout template needs a name and a root.");
        }

        var slots = new List<int>();
        var root = BuildNode(dto.Root, slots, dto.Name);

        if (dto.PhotoCount < 1 || !slots.Order().SequenceEqual(Enumerable.Range(1, dto.PhotoCount)))
        {
            throw new InvalidDataException(
                $"Layout template '{dto.Name}': slots must be numbered 1 to {dto.PhotoCount}, each used exactly once.");
        }

        return new LayoutTemplate(dto.Name, dto.Description ?? string.Empty, dto.PhotoCount, root);
    }

    private static IReadOnlyList<LayoutTemplate> LoadBuiltIn()
    {
        var assembly = typeof(LayoutTemplates).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                        n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal);

        var templates = new List<LayoutTemplate>();
        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidDataException($"Embedded layout template '{name}' could not be opened.");
            using var reader = new StreamReader(stream);
            templates.Add(Parse(reader.ReadToEnd()));
        }

        return templates;
    }

    private static LayoutNode BuildNode(NodeDto dto, List<int> slots, string templateName)
    {
        if (dto.Slot is { } slot)
        {
            if (dto.Split is not null || dto.Children is not null)
            {
                throw new InvalidDataException($"Layout template '{templateName}': a slot cannot also be a split.");
            }

            slots.Add(slot);
            return new SlotNode(slot);
        }

        var direction = dto.Split?.ToLowerInvariant() switch
        {
            "rows" => SplitDirection.Rows,
            "columns" => SplitDirection.Columns,
            _ => throw new InvalidDataException(
                $"Layout template '{templateName}': a node must be a slot or a split of \"rows\" or \"columns\"."),
        };

        if (dto.Children is not { Count: > 0 })
        {
            throw new InvalidDataException($"Layout template '{templateName}': a split needs at least one child.");
        }

        var children = new List<LayoutNode>(dto.Children.Count);
        foreach (var child in dto.Children)
        {
            children.Add(BuildNode(child, slots, templateName));
        }

        return new SplitNode(direction, children);
    }

    private sealed class TemplateDto
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public int PhotoCount { get; set; }

        public NodeDto? Root { get; set; }
    }

    private sealed class NodeDto
    {
        public int? Slot { get; set; }

        public string? Split { get; set; }

        public List<NodeDto>? Children { get; set; }
    }
}
