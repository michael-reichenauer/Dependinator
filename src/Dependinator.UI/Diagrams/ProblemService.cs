using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

enum ProblemSeverity
{
    Error,
    Warning,
}

enum ProblemKind
{
    Cycles,
    RuleViolations,
}

// One row of the problem indicator's list: a kind of problem, how many there are, and the text
// the row shows.
record ProblemGroup(ProblemKind Kind, ProblemSeverity Severity, int Count, string Text);

record ProblemSummary(IReadOnlyList<ProblemGroup> Groups)
{
    public static readonly ProblemSummary None = new([]);

    public int ErrorCount => Groups.Where(g => g.Severity == ProblemSeverity.Error).Sum(g => g.Count);
    public int WarningCount => Groups.Where(g => g.Severity == ProblemSeverity.Warning).Sum(g => g.Count);
    public bool IsEmpty => Groups.Count == 0;
}

// Collects what is wrong in the model for the problem indicator in the app bar, so the user sees
// that there are cycles or broken architecture rules without opening their panels: the cycles
// that are open or marked to fix later (accepted ones are intended, so not problems), and the
// rules that have violations, counted per rule (the Rules panel lists the dependencies).
// Rules whose nodes are gone are not counted. The summary is cached per model version; unlike
// the rule service it ignores the line count, which grows while zooming without changing what
// the counts are.
interface IProblemService
{
    ProblemSummary GetSummary();
}

[Scoped]
class ProblemService(IModelMgr modelMgr, ICycleService cycleService, IRuleService ruleService) : IProblemService
{
    (int Structure, int Nodes, int Links, int Accepted, int Rules) cachedFor = (-1, -1, -1, -1, -1);
    ProblemSummary summary = ProblemSummary.None;

    public ProblemSummary GetSummary()
    {
        var key = modelMgr.WithModel(m =>
            (m.StructureVersion, m.Nodes.Count, m.Links.Count, m.AcceptedCyclesVersion, m.RulesVersion)
        );
        if (key == cachedFor)
            return summary;

        summary = Compute();
        cachedFor = key;
        return summary;
    }

    ProblemSummary Compute()
    {
        List<ProblemGroup> groups = [];

        var open = cycleService.GetCycles().Count;
        var toFix = cycleService.GetCyclesToFix().Count(r => r.StillOccurs);
        if (open + toFix > 0)
            groups.Add(new(ProblemKind.Cycles, ProblemSeverity.Warning, open + toFix, CyclesText(open, toFix)));

        var broken = ruleService.GetReports().Where(r => r.Violations.Count > 0).ToList();
        if (broken.Count > 0)
        {
            var dependencies = broken.Sum(r => r.Violations.Count);
            groups.Add(
                new(
                    ProblemKind.RuleViolations,
                    ProblemSeverity.Warning,
                    broken.Count,
                    $"{Count(broken.Count, "broken architecture rule")} ({Count(dependencies, "dependency", "dependencies")})"
                )
            );
        }

        return groups.Count == 0 ? ProblemSummary.None : new(groups);
    }

    static string CyclesText(int open, int toFix) =>
        toFix == 0 ? Count(open, "cycle")
        : open == 0 ? $"{Count(toFix, "cycle")} to fix later"
        : $"{Count(open + toFix, "cycle")}, {toFix} to fix later";

    static string Count(int count, string singular, string? plural = null) =>
        $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
