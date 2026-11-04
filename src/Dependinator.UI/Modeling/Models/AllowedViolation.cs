namespace Dependinator.UI.Modeling.Models;

// A dependency that breaks an architecture rule and that the user allowed, with why, or marked to
// fix later (IsToFix), with the planned change as its Reason: the rule
// ("From must not use To") and the units at both ends of the dependency (a type using a type), by
// name like the rule itself, so it survives re-parses and travels with the model
// (ModelDto.AllowedViolations). An allowed dependency is not reported as a violation, nor is one to
// fix (it is listed under the rule's To fix and still drawn as breaking it, but not counted as a
// problem: it is known); one that is gone is kept, for the user to remove.
record AllowedViolation(
    string FromName,
    string ToName,
    string SourceName,
    string TargetName,
    string Reason,
    bool IsToFix = false
)
{
    public bool IsFor(ArchitectureRule rule) => FromName == rule.FromName && ToName == rule.ToName;

    public bool IsSameDependency(AllowedViolation other) =>
        FromName == other.FromName
        && ToName == other.ToName
        && SourceName == other.SourceName
        && TargetName == other.TargetName;
}
