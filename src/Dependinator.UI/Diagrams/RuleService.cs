using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

// One offending dependency: a unit (type, or other non-member node) inside the rule's "from"
// side that uses a unit inside its "to" side, with the number of links behind it. Names are the
// short ones for the row, the long ones for its tooltip; NodeNames identify the units in an
// AllowedViolation.
record RuleViolation(
    NodeId SourceId,
    string SourceName,
    string SourceLongName,
    NodeId TargetId,
    string TargetName,
    string TargetLongName,
    int LinkCount
)
{
    public string SourceNodeName { get; init; } = "";
    public string TargetNodeName { get; init; } = "";
}

// A dependency the user allowed under a rule, with its current link count; gone when no link
// makes it any more (it is kept, for the user to remove). SourceId is null when that node is gone.
record AllowedReport(AllowedViolation Allowed, string SourceName, string TargetName, NodeId? SourceId, int LinkCount)
{
    public bool IsGone => LinkCount == 0;
}

// A rule with its evaluation: the names as the user sees them, whether both nodes exist in the
// model, the violations found (empty for an unresolved rule), and the dependencies allowed under it.
record RuleReport(
    ArchitectureRule Rule,
    string FromDisplay,
    string ToDisplay,
    bool IsResolved,
    IReadOnlyList<RuleViolation> Violations
)
{
    public IReadOnlyList<AllowedReport> Allowed { get; init; } = [];
}

// Evaluates the model's architecture rules ("X must not depend on Y"): every link from inside
// X to inside Y is a violation, listed per type pair and drawn highlighted in the diagram while
// the rules panel is open, unless the user allowed that type pair, with why. Adding and removing
// rules and allowed dependencies goes through the command stack (undoable, saved with the model).
// Results are cached per structure and rules version.
interface IRuleService
{
    IReadOnlyList<RuleReport> GetReports();
    int ViolationCount { get; }

    // The lines carrying violating links (at every zoom level), for highlighting.
    IReadOnlySet<LineId> GetViolatingLineIds();

    // Adds "from must not use to"; an error says why a pair is not a rule (same node, exists).
    Result AddRule(NodeId fromId, NodeId toId);
    void RemoveRule(ArchitectureRule rule);

    // Allows a dependency that breaks the rule, with why: it is no longer reported.
    void Allow(ArchitectureRule rule, RuleViolation violation, string reason);

    // Stops allowing it: reported again while it occurs.
    void Disallow(AllowedViolation allowed);

    void SetAllowedReason(AllowedViolation allowed, string reason);
}

[Scoped]
class RuleService(IModelMgr modelMgr, ICommandService commandService) : IRuleService
{
    // Lines are part of the key: cousin lines are created lazily while zooming (no structure
    // version bump), and the highlighted line set has to include them.
    (int Structure, int Nodes, int Links, int Lines, int Rules) cachedFor = (-1, -1, -1, -1, -1);
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

    public Result AddRule(NodeId fromId, NodeId toId)
    {
        ArchitectureRule? rule;
        using (var model = modelMgr.UseModel())
        {
            if (fromId == toId)
                return new Error("A rule needs two different nodes.");
            if (!model.Nodes.TryGetValue(fromId, out var from) || !model.Nodes.TryGetValue(toId, out var to))
                return new Error("One of the nodes is no longer in the model.");
            rule = new ArchitectureRule(from.Name, to.Name);
            if (model.Rules.Contains(rule))
                return new Error($"\"{from.DisplayName} must not use {to.DisplayName}\" is already a rule.");
        }
        commandService.Do(new AddRuleCommand(rule));
        return Result.Ok;
    }

    public void RemoveRule(ArchitectureRule rule) => commandService.Do(new RemoveRuleCommand(rule));

    public void Allow(ArchitectureRule rule, RuleViolation violation, string reason)
    {
        var allowed = new AllowedViolation(
            rule.FromName,
            rule.ToName,
            violation.SourceNodeName,
            violation.TargetNodeName,
            reason.Trim()
        );
        var replaced = modelMgr.WithModel(m => m.AllowedViolations.Where(a => a.IsSameDependency(allowed)).ToList());
        commandService.Do(new AllowViolationCommand([allowed], replaced));
    }

    public void Disallow(AllowedViolation allowed) => commandService.Do(new AllowViolationCommand([], [allowed]));

    public void SetAllowedReason(AllowedViolation allowed, string reason)
    {
        if (allowed.Reason == reason.Trim())
            return;
        commandService.Do(new AllowViolationCommand([allowed with { Reason = reason.Trim() }], [allowed]));
    }

    void EnsureUpToDate()
    {
        using var model = modelMgr.UseModel();
        var key = (model.StructureVersion, model.Nodes.Count, model.Links.Count, model.Lines.Count, model.RulesVersion);
        if (key == cachedFor)
            return;
        (reports, violatingLineIds) = Compute(model);
        cachedFor = key;
    }

    internal static (IReadOnlyList<RuleReport> Reports, IReadOnlySet<LineId> LineIds) Compute(IModel model) =>
        Compute(model, model.Rules);

    // Evaluates the given rules, whether the model has them or not (checking a rule before adding
    // it); allowed dependencies apply to the rules they were allowed under.
    internal static (IReadOnlyList<RuleReport> Reports, IReadOnlySet<LineId> LineIds) Compute(
        IModel model,
        IReadOnlyList<ArchitectureRule> rules
    )
    {
        if (rules.Count == 0)
            return ([], new HashSet<LineId>());

        var nodesByName = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var node in model.Nodes.Values)
            nodesByName.TryAdd(node.Name, node);

        var resolved = new List<(ArchitectureRule Rule, Node? From, Node? To)>();
        foreach (var rule in rules)
        {
            nodesByName.TryGetValue(rule.FromName, out var from);
            nodesByName.TryGetValue(rule.ToName, out var to);
            resolved.Add((rule, from, to));
        }

        // The allowed dependencies per rule, by the names of the units at both ends.
        var allowed = resolved
            .Select(r =>
                model
                    .AllowedViolations.Where(a => a.IsFor(r.Rule))
                    .GroupBy(a => (a.SourceName, a.TargetName))
                    .ToDictionary(g => g.Key, g => g.First())
            )
            .ToList();
        var allowedLinks = resolved.Select(_ => new Dictionary<(string, string), int>()).ToList();

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
                var names = (pair.Item1.Name, pair.Item2.Name);
                if (allowed[i].ContainsKey(names))
                {
                    allowedLinks[i][names] = allowedLinks[i].GetValueOrDefault(names) + 1;
                    continue;
                }
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
                    kv.Key.Item1.DisplayName,
                    kv.Key.Item1.LongName,
                    kv.Key.Item2.Id,
                    kv.Key.Item2.DisplayName,
                    kv.Key.Item2.LongName,
                    kv.Value.Links.Count
                )
                {
                    SourceNodeName = kv.Key.Item1.Name,
                    TargetNodeName = kv.Key.Item2.Name,
                })
                .ToList();
            var allowedList = allowed[i]
                .Values.Select(a =>
                {
                    var source = nodesByName.GetValueOrDefault(a.SourceName);
                    var target = nodesByName.GetValueOrDefault(a.TargetName);
                    return new AllowedReport(
                        a,
                        source?.DisplayName ?? a.SourceName,
                        target?.DisplayName ?? a.TargetName,
                        source?.Id,
                        allowedLinks[i].GetValueOrDefault((a.SourceName, a.TargetName))
                    );
                })
                .OrderBy(a => a.SourceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.TargetName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            reports.Add(
                new RuleReport(
                    rule,
                    from?.LongName ?? rule.FromName,
                    to?.LongName ?? rule.ToName,
                    from is not null && to is not null,
                    list
                )
                {
                    Allowed = allowedList,
                }
            );
        }
        return (reports, lineIds);
    }

    static bool IsAtOrInside(Node node, Node container) => node == container || node.Ancestors().Contains(container);
}

// What the rule review dialog decided: allow the dependency with the reason, or (editing an
// allowed one) stop allowing it.
record RuleReviewDialogResult(string Reason, bool IsDisallow);
