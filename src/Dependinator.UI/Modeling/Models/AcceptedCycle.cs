namespace Dependinator.UI.Modeling.Models;

// A circular dependency the user decided about: accepted as intended, with why, or marked to fix
// later (IsToFix), with the planned change as its Reason. Stored by the container and the sibling
// nodes in the cycle, by name like an ArchitectureRule, so it survives re-parses and travels with
// the model (ModelDto.AcceptedCycles). A cycle found later keeps the decision while its members
// are among these (it may shrink); one that a new node joins is reported again. A decision whose
// cycle is gone is kept and shown as no longer occurring, for the user to remove: a refactor may
// be half done, or a node renamed, and the reason would be lost with it.
record AcceptedCycle(string ContainerName, IReadOnlyList<string> MemberNames, string Reason, bool IsToFix = false)
{
    // Sorted and distinct, so the same cycle always compares equal.
    public IReadOnlyList<string> MemberNames { get; } = [.. MemberNames.Distinct().Order(StringComparer.Ordinal)];

    public bool IsSameCycle(AcceptedCycle other) =>
        ContainerName == other.ContainerName && MemberNames.SequenceEqual(other.MemberNames);

    public virtual bool Equals(AcceptedCycle? other) =>
        other is not null && IsSameCycle(other) && Reason == other.Reason && IsToFix == other.IsToFix;

    public override int GetHashCode() => HashCode.Combine(ContainerName, MemberNames.Count, Reason, IsToFix);
}
