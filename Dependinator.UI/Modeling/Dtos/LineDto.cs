using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Dtos;

[Serializable]
record LineDto
{
    public required string LineId { get; init; }
    public required IReadOnlyList<Pos> SegmentPoints { get; init; }
}
