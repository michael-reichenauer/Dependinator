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

    // User-selected icon name; omitted when the node uses the node-type default icon.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? IconName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsUserSetHidden { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsParentSetHidden { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsChildrenLayoutCustomized { get; set; }

    // True for user-drawn nodes; omitted for parsed nodes so their serialized form is unchanged.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsManual { get; set; }
}
