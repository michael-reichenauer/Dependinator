using System.Text.Json.Serialization;

namespace Dependinator.Models;

[Serializable]
record ModelDto
{
    public static string CurrentFormatVersion = "7";
    public string FormatVersion { get; init; } = CurrentFormatVersion;

    public required string Name { get; init; }
    public double Zoom { get; init; } = 0;
    public Pos Offset { get; init; } = Pos.None;
    public Rect ViewRect { get; init; } = Rect.None;

    public required IReadOnlyList<NodeDto> Nodes { get; init; }
    public required IReadOnlyList<LinkDto> Links { get; init; }
    public IReadOnlyList<LineDto> Lines { get; init; } = [];
}

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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsUserSetHidden { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsParentSetHidden { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsChildrenLayoutCustomized { get; set; }
}

[Serializable]
record NodePropertiesDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool? IsPrivate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool? IsModule { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? MemberType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public FileSpanDto? FileSpan { get; set; }
}

[Serializable]
record FileSpanDto(string Path, int StarLine, int EndLine);

[Serializable]
record LinkDto(string SourceName, string TargetName, string TargetType);

[Serializable]
record LineDto
{
    public required string LineId { get; init; }
    public required IReadOnlyList<Pos> SegmentPoints { get; init; }
}
