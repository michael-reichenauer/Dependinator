using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

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
// that there are cycles or broken architecture rules without opening their panels: the open
// cycles, as many as the Cycles panel lists (accepted ones are intended, and ones marked to fix
// later are already known), and the rules that have violations, counted per rule (the Rules
// panel lists the dependencies). Rules whose nodes are gone are not counted.
// The summary is cached per model version. Unlike the rule service it ignores the line count,
// which grows while zooming without changing the counts. While a parse or load is in progress the
// model changes with every batch, so the last summary is kept until the progress ends (which
// raises UIStateChanged), instead of searching the half-built model for cycles over and over.
interface IProblemService
{
    ProblemSummary GetSummary();
}

[Scoped]
class ProblemService(
    IModelMgr modelMgr,
    ICycleService cycleService,
    IRuleService ruleService,
    IProgressService progressService
) : IProblemService
{
    (int Structure, int Nodes, int Links, int Accepted, int Rules) cachedFor = (-1, -1, -1, -1, -1);
    ProblemSummary summary = ProblemSummary.None;

    public ProblemSummary GetSummary()
    {
        if (progressService.IsProminentActive || progressService.IsDiscreetActive)
            return summary;

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

        var cycles = cycleService.GetCycles().Count;
        if (cycles > 0)
            groups.Add(new(ProblemKind.Cycles, ProblemSeverity.Warning, cycles, Count(cycles, "cycle")));

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

    static string Count(int count, string singular, string? plural = null) =>
        $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
