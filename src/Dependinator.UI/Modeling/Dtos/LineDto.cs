using System.Text.Json.Serialization;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Dtos;

[Serializable]
record LineDto
{
    public required string LineId { get; init; }
    public required IReadOnlyList<Pos> SegmentPoints { get; init; }

    // Always written on save; null only in caches from before auto-routing existed, where all
    // segment points were user-placed.
    public bool? IsSegmentsUserSet { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Description { get; init; }

    // The user's label on the line, and (only together with it) the endpoint node names: line
    // ids are hashes, so a label kept for a line that does not exist right now (a split line
    // after a reload) needs the names to tell when its nodes are gone for good.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Label { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? LabelDescription { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Source { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Target { get; init; }
}
