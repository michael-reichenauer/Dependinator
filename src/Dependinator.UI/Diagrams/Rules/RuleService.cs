using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams.Rules;

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

// A dependency marked to fix later under a rule, with its planned change (Planned.Reason): the
// dependency as it is now (null once no link makes it: it may be fixed), for its row and fix prompt.
record ToFixReport(AllowedViolation Planned, string SourceName, string TargetName, RuleViolation? Violation)
{
    public bool IsGone => Violation is null;
}

// A dismissed rule with the names as the user sees them (the stored ones for a node that is gone).
// Text is the rule in words, as RuleReport.Text has it.
record DismissedReport(DismissedRule Dismissed, string Text);

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

    // The dependencies marked to fix later: not violations (they are known), but still drawn.
    public IReadOnlyList<ToFixReport> ToFix { get; init; } = [];

    // The rule's nodes, null for one that is not in the model.
    public NodeId? FromId { get; init; }
    public NodeId? ToId { get; init; }

    // The rule in words, with the nodes' long names ("A must not use B", "only A may use B",
    // "layers A > B > C ..."), and its nodes in order (two, or the layers top first).
    public string Text { get; init; } = "";
    public IReadOnlyList<RuleNode> Nodes { get; init; } = [];
}

// One node of a rule: its name, the name the user sees, and its id while it is in the model.
record RuleNode(string Name, string Display, NodeId? Id);

// Evaluates the model's architecture rules ("X must not depend on Y", "only X may use Y", layers):
// every link that breaks one (see IsBreaking) is a violation, listed per type pair and drawn highlighted in the diagram while
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

    // Adds a rule of the kind over the nodes (from and to, the layers top first, or the one node); an error says
    // why they make no rule (too few, the same node twice, one inside another, exists).
    Result AddRule(RuleKind kind, IReadOnlyList<NodeId> nodeIds);

    // Adds the rules whose nodes are in the model, neither inside the other, and that are not
    // rules yet, as one undo step; the number added.
    int AddRules(IReadOnlyList<ArchitectureRule> rules);

    // Removes the rule and remembers it as dismissed, so Suggest rules does not offer it again.
    void RemoveRule(ArchitectureRule rule);

    // Changes why the rule exists.
    void SetRuleReason(ArchitectureRule rule, string reason);

    // The rules the user does not want suggested again, with their display names, as the Rules
    // panel lists them.
    IReadOnlyList<DismissedReport> GetDismissed();

    // Remembers the rule as not to be suggested again, with why.
    void Dismiss(ArchitectureRule rule, string reason);

    // Forgets a dismissal: Suggest rules may offer the rule again.
    void Undismiss(DismissedRule dismissed);

    // Allows a dependency that breaks the rule, with why: it is no longer reported.
    void Allow(ArchitectureRule rule, RuleViolation violation, string reason);

    // Stops allowing it: reported again while it occurs.
    void Disallow(AllowedViolation allowed);

    void SetAllowedReason(AllowedViolation allowed, string reason);

    // Marks the dependency to fix later, with the planned change: it is listed under the rule's To
    // fix instead of its violations, and no longer counted as a problem.
    void FixLater(ArchitectureRule rule, RuleViolation violation, string plannedChange);
}

[Scoped]
class RuleService(IModelMgr modelMgr, ICommandService commandService) : IRuleService
{
    // Lines are part of the key: cousin lines are created lazily while zooming (no structure
    // version bump), and the highlighted line set has to include them.
    // Accepted cycles are part of the key for "no cycles inside" rules, which skip them.
    (int Structure, int Nodes, int Links, int Lines, int Rules, int AcceptedCycles) cachedFor = (
        -1,
        -1,
        -1,
        -1,
        -1,
        -1
    );
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

    public Result AddRule(NodeId fromId, NodeId toId) => AddRule(RuleKind.MustNotUse, [fromId, toId]);

    public Result AddRule(RuleKind kind, IReadOnlyList<NodeId> nodeIds)
    {
        ArchitectureRule? rule;
        using (var model = modelMgr.UseModel())
        {
            List<Node> nodes = [];
            foreach (var id in nodeIds)
            {
                if (!model.Nodes.TryGetValue(id, out var node))
                    return new Error("One of the nodes is no longer in the model.");
                nodes.Add(node);
            }
            if (ShapeError(kind, nodes) is { } shapeError)
                return shapeError;
            rule = kind switch
            {
                RuleKind.Layers => ArchitectureRule.ForLayers([.. nodes.Select(n => n.Name)]),
                _ when ArchitectureRule.IsOneNodeKind(kind) => ArchitectureRule.ForNode(kind, nodes[0].Name),
                _ => new ArchitectureRule(nodes[0].Name, nodes[1].Name, Kind: kind),
            };
            if (model.Rules.Contains(rule))
                return new Error(
                    $"\"{rule.Describe(n => model.Nodes[NodeId.FromName(n)].DisplayName)}\" is already a rule."
                );
        }
        commandService.Do(new AddRuleCommand(rule));
        return Result.Ok;
    }

    // Why the nodes cannot make a rule of the kind, null when they can: the one check the Rules
    // panel, Suggest rules and the MCP check_rule share. "Must not use" may name a node inside the
    // other (a part that must not use the whole), as it always could; the other kinds would
    // contradict themselves. Members count for their type, so none may share one.
    public static Error? ShapeError(RuleKind kind, IReadOnlyList<Node> nodes)
    {
        var isOneNode = ArchitectureRule.IsOneNodeKind(kind);
        if (isOneNode ? nodes.Count != 1 : nodes.Count < 2 || (kind != RuleKind.Layers && nodes.Count != 2))
            return new Error(
                isOneNode ? "This kind of rule takes one node."
                : kind == RuleKind.Layers ? "Layers need at least two nodes."
                : "This kind of rule takes two nodes."
            );
        if (nodes.Distinct().Count() != nodes.Count)
            return new Error("A rule needs different nodes.");
        if (isOneNode)
        {
            var node = nodes[0];
            if (node.IsRoot || node.IsExternal())
                return new Error($"A rule about {node.DisplayName} would say nothing: pick a part of the solution.");
            if (node.Type.IsMember)
                return new Error($"{node.DisplayName} is a member: pick a type or one of its containers.");
            if (kind == RuleKind.SiblingsIndependent && node.Type.IsType)
                return new Error($"The parts of {node.DisplayName} are its members: pick a container of types.");
            return null;
        }
        foreach (var a in nodes)
        foreach (var b in nodes)
        {
            if (a == b)
                continue;
            if (kind != RuleKind.MustNotUse && a.IsAtOrInside(b))
                return new Error($"{a.DisplayName} is inside {b.DisplayName}: pick nodes beside each other.");
            if (UnitGraph.UnitOf(a) == UnitGraph.UnitOf(b))
                return new Error(
                    $"Rules apply to types and their containers: {a.DisplayName} and {b.DisplayName} are in one type."
                );
        }
        return null;
    }

    public int AddRules(IReadOnlyList<ArchitectureRule> rules)
    {
        List<Command> commands = [];
        using (var model = modelMgr.UseModel())
        {
            var added = new HashSet<ArchitectureRule>(model.Rules);
            foreach (var rule in rules)
            {
                var nodes = rule.NodeNames.Select(n => model.Nodes.GetValueOrDefault(NodeId.FromName(n))).ToList();
                if (nodes.All(n => n is not null) && ShapeError(rule.Kind, nodes!) is null && added.Add(rule))
                    commands.Add(new AddRuleCommand(rule));
            }
        }
        if (commands.Count > 0)
            commandService.Do(commands.Count == 1 ? commands[0] : new CompositeCommand([.. commands]));
        return commands.Count;
    }

    public void RemoveRule(ArchitectureRule rule) => commandService.Do(new RemoveRuleCommand(rule));

    public void SetRuleReason(ArchitectureRule rule, string reason)
    {
        var current = modelMgr.WithModel(m => m.Rules.FirstOrDefault(r => r == rule));
        if (current is null || current.Reason == reason.Trim())
            return;
        commandService.Do(new SetRuleReasonCommand(current, current with { Reason = reason.Trim() }));
    }

    public IReadOnlyList<DismissedReport> GetDismissed()
    {
        using var model = modelMgr.UseModel();
        string Display(string name) =>
            model.Nodes.TryGetValue(NodeId.FromName(name), out var node) ? node.LongName : name;
        return [.. model.DismissedRules.Select(d => new DismissedReport(d, d.Rule.Describe(Display)))];
    }

    public void Dismiss(ArchitectureRule rule, string reason) =>
        commandService.Do(new DismissRuleCommand([DismissedRule.Of(rule, reason.Trim())], []));

    public void Undismiss(DismissedRule dismissed) => commandService.Do(new DismissRuleCommand([], [dismissed]));

    public void Allow(ArchitectureRule rule, RuleViolation violation, string reason) =>
        Allow(rule, violation, reason, isToFix: false);

    void Allow(ArchitectureRule rule, RuleViolation violation, string reason, bool isToFix)
    {
        var allowed = new AllowedViolation(
            rule.FromName,
            rule.ToName,
            violation.SourceNodeName,
            violation.TargetNodeName,
            reason.Trim(),
            isToFix,
            rule.Kind,
            rule.LayersKey
        );
        var replaced = modelMgr.WithModel(m => m.AllowedViolations.Where(a => a.IsSameDependency(allowed)).ToList());
        commandService.Do(new AllowViolationCommand([allowed], replaced));
    }

    public void FixLater(ArchitectureRule rule, RuleViolation violation, string plannedChange) =>
        Allow(rule, violation, plannedChange, isToFix: true);

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
        var key = (
            model.StructureVersion,
            model.Nodes.Count,
            model.Links.Count,
            model.Lines.Count,
            model.RulesVersion,
            model.AcceptedCyclesVersion
        );
        if (key == cachedFor)
            return;
        var cycles = model.Rules.Any(r => r.Kind == RuleKind.NoCycles) ? CachedCycleIndexes(model) : null;
        (reports, violatingLineIds) = Compute(model, model.Rules, cycles);
        cachedFor = key;
    }

    internal static (IReadOnlyList<RuleReport> Reports, IReadOnlySet<LineId> LineIds) Compute(IModel model) =>
        Compute(model, model.Rules);

    // Evaluates the given rules, whether the model has them or not (checking a rule before adding
    // it); allowed dependencies apply to the rules they were allowed under.
    // The cycle indexes change with the structure and the accepted cycles, not with the lines that
    // zooming creates (part of the rules' key), so they are cached apart.
    (int Structure, int Nodes, int Links, int AcceptedCycles) cyclesCachedFor = (-1, -1, -1, -1);
    IReadOnlyDictionary<NodeId, int> cycleIndexes = new Dictionary<NodeId, int>();

    IReadOnlyDictionary<NodeId, int> CachedCycleIndexes(IModel model)
    {
        var key = (model.StructureVersion, model.Nodes.Count, model.Links.Count, model.AcceptedCyclesVersion);
        if (key != cyclesCachedFor)
            (cycleIndexes, cyclesCachedFor) = (CycleIndexes(model), key);
        return cycleIndexes;
    }

    internal static (IReadOnlyList<RuleReport> Reports, IReadOnlySet<LineId> LineIds) Compute(
        IModel model,
        IReadOnlyList<ArchitectureRule> rules,
        IReadOnlyDictionary<NodeId, int>? cycleIndexes = null
    )
    {
        if (rules.Count == 0)
            return ([], new HashSet<LineId>());

        var nodesByName = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var node in model.Nodes.Values)
            nodesByName.TryAdd(node.Name, node);
        var cycleOf =
            cycleIndexes
            ?? (rules.Any(r => r.Kind == RuleKind.NoCycles) ? CycleIndexes(model) : new Dictionary<NodeId, int>());

        var resolved = new List<(ArchitectureRule Rule, Node? From, Node? To, Node?[] Nodes)>();
        foreach (var rule in rules)
        {
            var nodes = rule.NodeNames.Select(n => nodesByName.GetValueOrDefault(n)).ToArray();
            nodesByName.TryGetValue(rule.FromName, out var from);
            nodesByName.TryGetValue(rule.ToName, out var to);
            // From and To stand for the whole rule in the loop below: null unless all its nodes are there.
            var isResolved = nodes.All(n => n is not null);
            resolved.Add((rule, isResolved ? from : null, isResolved ? to : null, nodes));
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
        var toFixLinks = resolved.Select(_ => new Dictionary<(string, string), (Node, Node, List<Link>)>()).ToList();

        // Violations per rule, grouped by the units at both ends (a method calling a method is
        // the class depending on the class).
        var violations = resolved.Select(_ => new Dictionary<(Node, Node), (List<Link> Links, int Order)>()).ToList();
        var lineIds = new HashSet<LineId>();
        foreach (var link in model.Links.Values)
        {
            for (var i = 0; i < resolved.Count; i++)
            {
                var (rule, from, to, nodes) = resolved[i];
                if (from is null || to is null || !IsBreaking(rule.Kind, nodes, link, cycleOf))
                    continue;
                var pair = (UnitGraph.UnitOf(link.Source), UnitGraph.UnitOf(link.Target));
                var names = (pair.Item1.Name, pair.Item2.Name);
                if (allowed[i].TryGetValue(names, out var allowedOne))
                {
                    if (allowedOne.IsToFix)
                    {
                        // Still drawn as breaking the rule, though it is known.
                        if (!toFixLinks[i].TryGetValue(names, out var toFix))
                            toFixLinks[i][names] = toFix = (pair.Item1, pair.Item2, []);
                        toFix.Item3.Add(link);
                        foreach (var line in link.Lines)
                            lineIds.Add(line.Id);
                    }
                    else
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
            var (rule, from, _, nodes) = resolved[i];
            var list = violations[i]
                .OrderBy(kv => kv.Key.Item1.LongName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(kv => kv.Key.Item2.LongName, StringComparer.OrdinalIgnoreCase)
                .Select(kv => ToViolation(kv.Key.Item1, kv.Key.Item2, kv.Value.Links.Count))
                .ToList();
            var toFixList = allowed[i]
                .Values.Where(a => a.IsToFix)
                .Select(a =>
                {
                    if (toFixLinks[i].TryGetValue((a.SourceName, a.TargetName), out var found))
                    {
                        var violation = ToViolation(found.Item1, found.Item2, found.Item3.Count);
                        return new ToFixReport(a, violation.SourceName, violation.TargetName, violation);
                    }
                    var source = nodesByName.GetValueOrDefault(a.SourceName);
                    var target = nodesByName.GetValueOrDefault(a.TargetName);
                    return new ToFixReport(
                        a,
                        source?.DisplayName ?? a.SourceName,
                        target?.DisplayName ?? a.TargetName,
                        null
                    );
                })
                .OrderBy(a => a.SourceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.TargetName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var allowedList = allowed[i]
                .Values.Where(a => !a.IsToFix)
                .Select(a =>
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
                    nodes[0]?.LongName ?? rule.FromName,
                    nodes[^1]?.LongName ?? rule.ToName,
                    from is not null,
                    list
                )
                {
                    Text = rule.Describe(n => nodesByName.GetValueOrDefault(n)?.LongName ?? n),
                    Nodes =
                    [
                        .. rule.NodeNames.Select((n, j) => new RuleNode(n, nodes[j]?.LongName ?? n, nodes[j]?.Id)),
                    ],
                    Allowed = allowedList,
                    ToFix = toFixList,
                    FromId = nodes[0]?.Id,
                    ToId = nodes[^1]?.Id,
                }
            );
        }
        return (reports, lineIds);
    }

    // Whether the link breaks the rule: "from must not use to" any link from inside from to inside
    // to; "only from may use to" any link into to from outside both; layers any link from inside a
    // layer to inside a layer above it (layers are listed top first). The rule's nodes are all in
    // the model (Compute skips an unresolved rule). "Is independent" any link from inside it to
    // anything outside it but externals; "no cycles inside" any link between two members of one
    // cycle (cycleOf, see CycleIndexes) whose container is at or inside the node; "the parts of it are
    // independent" any link from inside one of its children to inside another.
    static bool IsBreaking(RuleKind kind, Node?[] nodes, Link link, IReadOnlyDictionary<NodeId, int> cycleOf)
    {
        switch (kind)
        {
            case RuleKind.Independent:
                return link.Source.IsAtOrInside(nodes[0]!)
                    && !link.Target.IsAtOrInside(nodes[0]!)
                    && !link.Target.IsExternal();
            case RuleKind.SiblingsIndependent:
                return ChildOn(link.Source, nodes[0]!) is { } sourceChild
                    && ChildOn(link.Target, nodes[0]!) is { } targetChild
                    && sourceChild != targetChild;
            case RuleKind.NoCycles:
                return Cycles.CycleService.TryGetSiblingsUnderCommonAncestor(
                        link.Source,
                        link.Target,
                        out var from,
                        out var to
                    )
                    && from.Parent.IsAtOrInside(nodes[0]!)
                    && cycleOf.TryGetValue(from.Id, out var fromCycle)
                    && cycleOf.TryGetValue(to.Id, out var toCycle)
                    && fromCycle == toCycle;
            case RuleKind.OnlyMayUse:
                return link.Target.IsAtOrInside(nodes[1]!)
                    && !link.Source.IsAtOrInside(nodes[1]!)
                    && !link.Source.IsAtOrInside(nodes[0]!);
            case RuleKind.Layers:
                var sourceLayer = Array.FindIndex(nodes, n => link.Source.IsAtOrInside(n!));
                var targetLayer = Array.FindIndex(nodes, n => link.Target.IsAtOrInside(n!));
                return sourceLayer >= 0 && targetLayer >= 0 && targetLayer < sourceLayer;
            default:
                return link.Source.IsAtOrInside(nodes[0]!) && link.Target.IsAtOrInside(nodes[1]!);
        }
    }

    // The child of the container that the node is at or inside; null for the container itself.
    static Node? ChildOn(Node node, Node container)
    {
        for (var n = node; n.Parent is not null; n = n.Parent)
        {
            if (n.Parent == container)
                return n;
        }
        return null;
    }

    // The cycle each cycle member is in, by an index per cycle: the open cycles the Cycles panel
    // lists, not those the user accepted or marked to fix there (decisions both share).
    static Dictionary<NodeId, int> CycleIndexes(IModel model)
    {
        var (open, _, _, _, _) = Cycles.CycleService.Compute(model);
        var cycleOf = new Dictionary<NodeId, int>();
        var index = 0;
        foreach (var cycle in open)
        {
            foreach (var member in cycle.Members)
                cycleOf[member.Id] = index;
            index++;
        }
        return cycleOf;
    }

    static RuleViolation ToViolation(Node source, Node target, int linkCount) =>
        new(source.Id, source.DisplayName, source.LongName, target.Id, target.DisplayName, target.LongName, linkCount)
        {
            SourceNodeName = source.Name,
            TargetNodeName = target.Name,
        };
}

// What the rule review dialog decided: allow the dependency with the reason, mark it to fix later
// (IsFixLater, the reason is the planned change), or (editing an allowed one) stop allowing it.
record RuleReviewDialogResult(string Reason, bool IsDisallow, bool IsFixLater = false);
