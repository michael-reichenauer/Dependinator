namespace Dependinator.UI.Modeling.Models;

// The kinds of architecture rule; each one comes down to which dependencies break it (RuleService).
enum RuleKind
{
    // "From must not use To": nothing at or inside From may depend on anything at or inside To.
    MustNotUse,

    // "Only From may use To": nothing outside both From and To may depend on anything inside To.
    OnlyMayUse,

    // "Layers": LayerNames, top first; code in a layer may use the layers below it, never one above.
    Layers,

    // "From is independent": nothing at or inside From may depend on anything else in the solution,
    // only on externals (packages and framework). One node: To is From.
    Independent,

    // "No cycles inside From": no cycle at or inside From (the cycles the Cycles panel finds there,
    // but those accepted or marked to fix there). One node: To is From.
    NoCycles,

    // "The parts of From are independent": no child of From (with what is inside it) may depend on
    // another child of From, say feature folders. One node: To is From.
    SiblingsIndependent,
}

// An architecture rule the user declared (see RuleKind). Nodes are referred to by name, so a rule
// survives re-parses and travels with the model (ModelDto.Rules); a rule whose node is gone is kept
// and shown as unresolved. A layers rule names its top layer as From and its bottom one as To.
// Reason says why the rule exists (an AI's why when it suggested the rule, or the user's words); it
// is not part of the rule's identity, so two rules on the same nodes are the same rule whatever
// their reasons.
record ArchitectureRule(
    string FromName,
    string ToName,
    string Reason = "",
    RuleKind Kind = RuleKind.MustNotUse,
    IReadOnlyList<string>? LayerNames = null
)
{
    // A layers rule from its layers, top first.
    public static ArchitectureRule ForLayers(IReadOnlyList<string> layerNames, string reason = "") =>
        new(layerNames[0], layerNames[^1], reason, RuleKind.Layers, layerNames);

    // A rule about one node ("is independent", "no cycles inside").
    public static ArchitectureRule ForNode(RuleKind kind, string nodeName, string reason = "") =>
        new(nodeName, nodeName, reason, kind);

    public static bool IsOneNodeKind(RuleKind kind) =>
        kind is RuleKind.Independent or RuleKind.NoCycles or RuleKind.SiblingsIndependent;

    // A rule of the kind over its nodes (from and to, the layers top first, or the one node), as
    // the AI and the MCP tools name them; null when the nodes do not fit the kind.
    public static ArchitectureRule? Of(RuleKind kind, IReadOnlyList<string> nodeNames, string reason = "") =>
        kind switch
        {
            RuleKind.Layers when nodeNames.Count >= 2 => ForLayers(nodeNames, reason),
            _ when IsOneNodeKind(kind) && nodeNames.Count == 1 => ForNode(kind, nodeNames[0], reason),
            RuleKind.MustNotUse or RuleKind.OnlyMayUse when nodeNames.Count == 2 => new(
                nodeNames[0],
                nodeNames[1],
                reason,
                kind
            ),
            _ => null,
        };

    // The kinds by the names an AI and the MCP tools use for them.
    public static readonly IReadOnlyDictionary<string, RuleKind> KindNames = new Dictionary<string, RuleKind>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["must_not_use"] = RuleKind.MustNotUse,
        ["only_may_use"] = RuleKind.OnlyMayUse,
        ["layers"] = RuleKind.Layers,
        ["independent"] = RuleKind.Independent,
        ["no_cycles"] = RuleKind.NoCycles,
        ["siblings_independent"] = RuleKind.SiblingsIndependent,
    };

    public static string KindName(RuleKind kind) => KindNames.First(k => k.Value == kind).Key;

    // A layers rule's layers as one string, "" for the other kinds: tells layers rules with the same
    // top and bottom layers apart (AllowedViolation.LayersKey).
    public string LayersKey => Kind == RuleKind.Layers ? string.Join('\n', NodeNames) : "";

    // The rule's nodes in the order the rule names them.
    public IReadOnlyList<string> NodeNames =>
        Kind == RuleKind.Layers && LayerNames is { Count: > 0 } layers ? layers
        : IsOneNodeKind(Kind) ? [FromName]
        : [FromName, ToName];

    // The rule as text, with each node named by display (its name in backticks for a prompt, say).
    public string Describe(Func<string, string> display) =>
        Kind switch
        {
            RuleKind.OnlyMayUse => $"only {display(FromName)} may use {display(ToName)}",
            RuleKind.Independent => $"{display(FromName)} is independent (uses only externals)",
            RuleKind.NoCycles => $"no cycles inside {display(FromName)}",
            RuleKind.SiblingsIndependent => $"the parts of {display(FromName)} are independent of each other",
            RuleKind.Layers =>
                $"layers {string.Join(" > ", NodeNames.Select(display))} (each may use only the layers below it)",
            _ => $"{display(FromName)} must not use {display(ToName)}",
        };

    public virtual bool Equals(ArchitectureRule? other) =>
        other is not null
        && Kind == other.Kind
        && FromName == other.FromName
        && ToName == other.ToName
        && NodeNames.SequenceEqual(other.NodeNames);

    public override int GetHashCode() => HashCode.Combine(Kind, FromName, ToName, NodeNames.Count);
}

// A rule the user does not want suggested again: dismissed in Suggest rules, with an optional
// why, or removed from the rules. By name like the rule, saved with the model
// (ModelDto.DismissedRules, those of other kinds than "must not use" in KindDismissedRules), so
// the suggestions skip it in later sessions and after a re-parse. Rule is the rule without a reason.
record DismissedRule(ArchitectureRule Rule, string Reason = "")
{
    public static DismissedRule Of(ArchitectureRule rule, string reason = "") => new(rule with { Reason = "" }, reason);

    public bool IsFor(ArchitectureRule rule) => Rule == rule;

    public virtual bool Equals(DismissedRule? other) => other is not null && Rule == other.Rule;

    public override int GetHashCode() => Rule.GetHashCode();
}
