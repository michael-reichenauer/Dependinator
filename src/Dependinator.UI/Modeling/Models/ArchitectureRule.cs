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

    // The rule's nodes in the order the rule names them.
    // A layers rule's layers as one string, "" for the other kinds: tells layers rules with the same
    // top and bottom layers apart (AllowedViolation.LayersKey).
    public string LayersKey => Kind == RuleKind.Layers ? string.Join('\n', NodeNames) : "";

    public IReadOnlyList<string> NodeNames =>
        Kind == RuleKind.Layers && LayerNames is { Count: > 0 } layers ? layers : [FromName, ToName];

    // The rule as text, with each node named by display (its name in backticks for a prompt, say).
    public string Describe(Func<string, string> display) =>
        Kind switch
        {
            RuleKind.OnlyMayUse => $"only {display(FromName)} may use {display(ToName)}",
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
// (ModelDto.DismissedRules), so the suggestions skip it in later sessions and after a re-parse.
record DismissedRule(string FromName, string ToName, string Reason = "")
{
    public bool IsFor(string fromName, string toName) => FromName == fromName && ToName == toName;

    public virtual bool Equals(DismissedRule? other) => other is not null && IsFor(other.FromName, other.ToName);

    public override int GetHashCode() => HashCode.Combine(FromName, ToName);
}
