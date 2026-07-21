using Dependinator.UI.Shared.Types;

// Serializable data-transfer objects for persisting and loading the model to and from storage,
// including the versioned on-disk format.
namespace Dependinator.UI.Modeling.Dtos;

[Serializable]
record ModelDto
{
    public const string CurrentFormatVersion = "8";
    public string FormatVersion { get; init; } = CurrentFormatVersion;

    public required string Name { get; init; }
    public double Zoom { get; init; } = 0;
    public Pos Offset { get; init; } = Pos.None;
    public Rect ViewRect { get; init; } = Rect.None;

    public required IReadOnlyList<NodeDto> Nodes { get; init; }
    public required IReadOnlyList<LinkDto> Links { get; init; }
    public IReadOnlyList<LineDto> Lines { get; init; } = [];
}
