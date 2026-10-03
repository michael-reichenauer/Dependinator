using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

// One offending dependency: a unit (type, or other non-member node) inside the rule's "from"
// side that uses a unit inside its "to" side, with the number of links behind it. Names are the
// short ones for the row, the long ones for its tooltip.
record RuleViolation(
    NodeId SourceId,
    string SourceName,
    string SourceLongName,
    NodeId TargetId,
    string TargetName,
    string TargetLongName,
    int LinkCount
);

// A rule with its evaluation: the names as the user sees them, whether both nodes exist in the
// model, and the violations found (empty for an unresolved rule).
record RuleReport(
    ArchitectureRule Rule,
    string FromDisplay,
    string ToDisplay,
    bool IsResolved,
    IReadOnlyList<RuleViolation> Violations
);

// Evaluates the model's architecture rules ("X must not depend on Y"): every link from inside
// X to inside Y is a violation, listed per type pair and drawn highlighted in the diagram while
// the rules panel is open. Adding and removing rules goes through the command stack (undoable,
// saved with the model). Results are cached per structure and rules version.
interface IRuleService
{
    IReadOnlyList<RuleReport> GetReports();
    int ViolationCount { get; }

    // The lines carrying violating links (at every zoom level), for highlighting.
    IReadOnlySet<LineId> GetViolatingLineIds();

    void AddRule(NodeId fromId, NodeId toId);
    void RemoveRule(ArchitectureRule rule);
}

[Scoped]
class RuleService(IModelMgr modelMgr, ICommandService commandService) : IRuleService
{
    (int Structure, int Nodes, int Links, int Rules) cachedFor = (-1, -1, -1, -1);
    IReadOnlyList<RuleReport> reports = [];
    IReadOnlySet<LineId> violatingLineIds = new HashSet<LineId>();

    public IReadOnlyList<RuleReport> GetReports()
    {
        EnsureUpToDate();
        return reports;
    }

    public int ViolationCount
    {
        get
        {
            EnsureUpToDate();
            return reports.Sum(r => r.Violations.Count);
        }
    }

    public IReadOnlySet<LineId> GetViolatingLineIds()
    {
        EnsureUpToDate();
        return violatingLineIds;
    }

    public void AddRule(NodeId fromId, NodeId toId)
    {
        ArchitectureRule? rule;
        using (var model = modelMgr.UseModel())
        {
            if (
                fromId == toId
                || !model.Nodes.TryGetValue(fromId, out var from)
                || !model.Nodes.TryGetValue(toId, out var to)
            )
                return;
            rule = new ArchitectureRule(from.Name, to.Name);
            if (model.Rules.Contains(rule))
                return;
        }
        commandService.Do(new AddRuleCommand(rule));
    }

    public void RemoveRule(ArchitectureRule rule) => commandService.Do(new RemoveRuleCommand(rule));

    void EnsureUpToDate()
    {
        using var model = modelMgr.UseModel();
        var key = (model.StructureVersion, model.Nodes.Count, model.Links.Count, model.RulesVersion);
        if (key == cachedFor)
            return;
        (reports, violatingLineIds) = Compute(model);
        cachedFor = key;
    }

    internal static (IReadOnlyList<RuleReport> Reports, IReadOnlySet<LineId> LineIds) Compute(IModel model)
    {
        if (model.Rules.Count == 0)
            return ([], new HashSet<LineId>());

        var nodesByName = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var node in model.Nodes.Values)
            nodesByName.TryAdd(node.Name, node);

        var resolved = new List<(ArchitectureRule Rule, Node? From, Node? To)>();
        foreach (var rule in model.Rules)
        {
            nodesByName.TryGetValue(rule.FromName, out var from);
            nodesByName.TryGetValue(rule.ToName, out var to);
            resolved.Add((rule, from, to));
        }

        // Violations per rule, grouped by the units at both ends (a method calling a method is
        // the class depending on the class).
        var violations = resolved.Select(_ => new Dictionary<(Node, Node), (List<Link> Links, int Order)>()).ToList();
        var lineIds = new HashSet<LineId>();
        foreach (var link in model.Links.Values)
        {
            for (var i = 0; i < resolved.Count; i++)
            {
                var (_, from, to) = resolved[i];
                if (from is null || to is null)
                    continue;
                if (!IsAtOrInside(link.Source, from) || !IsAtOrInside(link.Target, to))
                    continue;
                var pair = (UnitGraph.UnitOf(link.Source), UnitGraph.UnitOf(link.Target));
                if (!violations[i].TryGetValue(pair, out var entry))
                    violations[i][pair] = entry = ([], violations[i].Count);
                entry.Links.Add(link);
                foreach (var line in link.Lines)
                    lineIds.Add(line.Id);
            }
        }

        var reports = new List<RuleReport>();
        for (var i = 0; i < resolved.Count; i++)
        {
            var (rule, from, to) = resolved[i];
            var list = violations[i]
                .OrderBy(kv => kv.Key.Item1.LongName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(kv => kv.Key.Item2.LongName, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new RuleViolation(
                    kv.Key.Item1.Id,
                    kv.Key.Item1.ShortName,
                    kv.Key.Item1.LongName,
                    kv.Key.Item2.Id,
                    kv.Key.Item2.ShortName,
                    kv.Key.Item2.LongName,
                    kv.Value.Links.Count
                ))
                .ToList();
            reports.Add(
                new RuleReport(
                    rule,
                    from?.LongName ?? rule.FromName,
                    to?.LongName ?? rule.ToName,
                    from is not null && to is not null,
                    list
                )
            );
        }
        return (reports, lineIds);
    }

    static bool IsAtOrInside(Node node, Node container) => node == container || node.Ancestors().Contains(container);
}
