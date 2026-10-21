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
    ParseErrors,
    Cycles,
    RuleViolations,
}

// One row of the problem indicator's list: a kind of problem, how many there are, the text the
// row shows, and for a parse problem the node it concerns (shown on a click), if it is in the model.
record ProblemGroup(ProblemKind Kind, ProblemSeverity Severity, int Count, string Text, NodeId? NodeId = null);

record ProblemSummary(IReadOnlyList<ProblemGroup> Groups)
{
    public static readonly ProblemSummary None = new([]);

    public int ErrorCount => Groups.Where(g => g.Severity == ProblemSeverity.Error).Sum(g => g.Count);
    public int WarningCount => Groups.Where(g => g.Severity == ProblemSeverity.Warning).Sum(g => g.Count);
    public bool IsEmpty => Groups.Count == 0;
}

// The problems in a node or anywhere inside it, for its badge in the diagram: errors (compile
// errors) and warnings (cycles inside it, dependencies from it that break a rule).
readonly record struct NodeProblems(int Errors, int Warnings)
{
    public static NodeProblems operator +(NodeProblems a, NodeProblems b) =>
        new(a.Errors + b.Errors, a.Warnings + b.Warnings);
}

// Collects what is wrong in the model for the problem indicator in the app bar, so the user sees
// it without opening a panel or the log: the errors the last parse reported (compile errors,
// one row per project or Bicep file, counted per error), the open cycles, as many as the Cycles
// panel lists (accepted ones are intended, and ones marked to fix later are already known), and
// the rules that have violations, counted per rule (the Rules panel lists the dependencies).
// Rules whose nodes are gone are not counted.
// The summary is cached per model version. Unlike the rule service it ignores the line count,
// which grows while zooming without changing the counts. While a parse or load is in progress the
// model changes with every batch, so the last summary is kept until the progress ends (which
// raises UIStateChanged), instead of searching the half-built model for cycles over and over.
interface IProblemService
{
    ProblemSummary GetSummary();

    // Per node, the problems in it or inside it (nodes without any are left out), so a badge
    // leads from the top level down to where they are. Cached like the summary, and the same
    // instance until the counts change.
    IReadOnlyDictionary<NodeId, NodeProblems> GetNodeProblems();
}

[Scoped]
class ProblemService(
    IModelMgr modelMgr,
    ICycleService cycleService,
    IRuleService ruleService,
    IProgressService progressService
) : IProblemService
{
    (int Structure, int Nodes, int Links, int Accepted, int Rules, int ParseProblems) cachedFor = (
        -1,
        -1,
        -1,
        -1,
        -1,
        -1
    );
    ProblemSummary summary = ProblemSummary.None;
    IReadOnlyDictionary<NodeId, NodeProblems> nodeProblems = new Dictionary<NodeId, NodeProblems>();

    public IReadOnlyDictionary<NodeId, NodeProblems> GetNodeProblems()
    {
        GetSummary();
        return nodeProblems;
    }

    public ProblemSummary GetSummary()
    {
        if (progressService.IsProminentActive || progressService.IsDiscreetActive)
            return summary;

        var key = modelMgr.WithModel(m =>
            (
                m.StructureVersion,
                m.Nodes.Count,
                m.Links.Count,
                m.AcceptedCyclesVersion,
                m.RulesVersion,
                m.ParseProblemsVersion
            )
        );
        if (key == cachedFor)
            return summary;

        summary = Compute();
        // Kept when unchanged, so a renderer can tell by reference that its badges are current.
        var newNodeProblems = ComputeNodeProblems();
        if (!IsSame(newNodeProblems, nodeProblems))
            nodeProblems = newNodeProblems;
        cachedFor = key;
        return summary;
    }

    ProblemSummary Compute()
    {
        List<ProblemGroup> groups = modelMgr.WithModel(m =>
            m.ParseProblems.Select(p => new ProblemGroup(
                    ProblemKind.ParseErrors,
                    ProblemSeverity.Error,
                    p.ErrorCount,
                    p.Message,
                    NodeIdIn(m, p.NodeName)
                ))
                .ToList()
        );

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

    // Each problem counts at the node it belongs to and at every ancestor above it (not the root,
    // which is never drawn). A cycle belongs to the container its members are in; one among the
    // top-level nodes, whose container is the root, to each of its members instead.
    IReadOnlyDictionary<NodeId, NodeProblems> ComputeNodeProblems()
    {
        List<(NodeId Id, NodeProblems Problems)> own = [];
        foreach (var group in summary.Groups.Where(g => g.Kind == ProblemKind.ParseErrors && g.NodeId is not null))
            own.Add((group.NodeId!, new(group.Count, 0)));
        foreach (var cycle in cycleService.GetCycles())
        {
            var isTopLevel = modelMgr.WithModel(m => m.Nodes.TryGetValue(cycle.ContainerId, out var c) && c.IsRoot);
            if (isTopLevel)
                own.AddRange(cycle.Members.Select(member => (member.Id, new NodeProblems(0, 1))));
            else
                own.Add((cycle.ContainerId, new(0, 1)));
        }
        foreach (var violation in ruleService.GetReports().SelectMany(r => r.Violations))
            own.Add((violation.SourceId, new(0, 1)));

        return modelMgr.WithModel(m =>
        {
            var result = new Dictionary<NodeId, NodeProblems>();
            foreach (var (id, problems) in own)
            {
                if (!m.Nodes.TryGetValue(id, out var node))
                    continue;
                for (var n = node; !n.IsRoot; n = n.Parent)
                    result[n.Id] = result.GetValueOrDefault(n.Id) + problems;
            }
            return result;
        });
    }

    static bool IsSame(IReadOnlyDictionary<NodeId, NodeProblems> a, IReadOnlyDictionary<NodeId, NodeProblems> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var other) && other == kv.Value);

    // A cached model's problems can name a node a later edit or parse removed.
    static NodeId? NodeIdIn(IModel model, string? nodeName) =>
        nodeName is not null && NodeId.FromName(nodeName) is var id && model.Nodes.ContainsKey(id) ? id : null;

    static string Count(int count, string singular, string? plural = null) =>
        $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
