using Dependinator.Shared.Types;

namespace Dependinator.Models.Persistence;

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
