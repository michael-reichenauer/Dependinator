using System.Text.Json.Serialization;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Dtos;

// When caching the node, the NodeDto contains the cached data
[Serializable]
record NodeDto
{
    public required string Name { get; init; }
    public required string ParentName { get; init; }
    public required string Type { get; init; }
    public NodePropertiesDto Properties { get; init; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Rect? Boundary { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double? Zoom { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Pos? Offset { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Color { get; init; }

    // User-selected container color; omitted when the node uses its auto-assigned Color.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? CustomColor { get; init; }

    // User-selected icon name; omitted when the node uses the node-type default icon.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? IconName { get; init; }

    // User-selected icon tint; omitted when the icon uses the default violet. Independent of
    // Color (the node container's palette color).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? IconColor { get; init; }

    // The user's display name; omitted when the node shows its name from the code.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Alias { get; init; }

    // The user's own NodeVisibility (Dimmed or Hidden); omitted when visible.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Visibility { get; init; }

    // The NodeVisibility inherited from the ancestors; omitted when visible.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ParentVisibility { get; init; }

    // Before dimming existed: true when Visibility is not visible. Still written so an older app
    // reading a newer model keeps those nodes hidden.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsUserSetHidden { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsParentSetHidden { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsChildrenLayoutCustomized { get; set; }

    // True while the children still need an automatic layout (laid out lazily when first shown),
    // so a save before that does not keep a stale layout; omitted once they are laid out.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsChildrenLayoutRequired { get; set; }

    // True for user-drawn nodes; omitted for parsed nodes so their serialized form is unchanged.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsManual { get; set; }

    // True for note annotations (rendered as a circle with a short id). Omitted for other nodes.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsNote { get; set; }

    // True for zones (user-drawn labeled background areas). Omitted for other nodes.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsZone { get; set; }
}
