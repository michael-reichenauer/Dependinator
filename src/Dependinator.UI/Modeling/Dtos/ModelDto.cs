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

    // Defaulted, so models saved before allowed violations existed still deserialize (no FormatVersion bump).
    public IReadOnlyList<AllowedViolationDto> AllowedViolations { get; init; } = [];

    // Defaulted, so models saved before accepted cycles existed still deserialize (no FormatVersion bump).
    public IReadOnlyList<AcceptedCycleDto> AcceptedCycles { get; init; } = [];

    // The problems the last parse found (compile errors), so the problem indicator shows them
    // for a model opened from the cache, before (or without) a new parse. Defaulted (no
    // FormatVersion bump).
    public IReadOnlyList<ParseProblemDto> ParseProblems { get; init; } = [];
}

// A problem the last parse found (see Dependinator.Core.Parsing.ParseProblem).
[Serializable]
record ParseProblemDto
{
    public required string Message { get; init; }
    public int ErrorCount { get; init; }
    public string? NodeName { get; init; }
}

// An architecture rule: "From" must not depend on "To" (node names, see ArchitectureRule).
[Serializable]
record RuleDto
{
    public required string From { get; init; }
    public required string To { get; init; }
}

// A dependency that breaks the rule "From must not use To" and that the user allowed, with why:
// the units at both ends (node names, see AllowedViolation).
[Serializable]
record AllowedViolationDto
{
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Source { get; init; }
    public required string Target { get; init; }
    public string Reason { get; init; } = "";
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
