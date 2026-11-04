namespace Dependinator.UI.Modeling.Models;

// An architecture rule the user declared: nothing at or inside the "from" node may depend on
// anything at or inside the "to" node. Nodes are referred to by name, so a rule survives
// re-parses and travels with the model (ModelDto.Rules); a rule whose node is gone is kept and
// shown as unresolved. Reason says why the rule exists (an AI's why when it suggested the rule,
// or the user's words); it is not part of the rule's identity, so two rules on the same nodes are
// the same rule whatever their reasons.
record ArchitectureRule(string FromName, string ToName, string Reason = "")
{
    public virtual bool Equals(ArchitectureRule? other) =>
        other is not null && FromName == other.FromName && ToName == other.ToName;

    public override int GetHashCode() => HashCode.Combine(FromName, ToName);
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
