using Dependinator.Shared.Types;

namespace Dependinator.Modeling.Dtos;

[Serializable]
record LineDto
{
    public required string LineId { get; init; }
    public required IReadOnlyList<Pos> SegmentPoints { get; init; }
}
