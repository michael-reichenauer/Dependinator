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
}
