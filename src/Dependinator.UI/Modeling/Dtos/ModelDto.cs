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

    // Defaulted, so models cached before this existed still deserialize (no FormatVersion bump).
    public bool IncludeTestProjects { get; init; }

    // The automatic layout version the model was saved with (NodeLayout.CurrentLayoutVersion);
    // 0 for models saved before it existed. Defaulted (no FormatVersion bump).
    public int LayoutVersion { get; init; }

    public required IReadOnlyList<NodeDto> Nodes { get; init; }
    public required IReadOnlyList<LinkDto> Links { get; init; }
    public IReadOnlyList<LineDto> Lines { get; init; } = [];

    // Defaulted, so models saved before rules existed still deserialize (no FormatVersion bump).
    public IReadOnlyList<RuleDto> Rules { get; init; } = [];

    // Defaulted, so models saved before accepted cycles existed still deserialize (no FormatVersion bump).
    public IReadOnlyList<AcceptedCycleDto> AcceptedCycles { get; init; } = [];
}

// An architecture rule: "From" must not depend on "To" (node names, see ArchitectureRule).
[Serializable]
record RuleDto
{
    public required string From { get; init; }
    public required string To { get; init; }
}

// A circular dependency the user accepted, with why, or marked to fix later, with the planned
// change (node names, see AcceptedCycle).
[Serializable]
record AcceptedCycleDto
{
    public required string Container { get; init; }
    public required IReadOnlyList<string> Members { get; init; }
    public string Reason { get; init; } = "";
    public bool IsToFix { get; init; }
}
