using Dependinator.Core.ModelQueries;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams.Ai;

// Answers IModelQueryService's questions on this scope's model (a browser tab, or the VS Code
// webview). The answers are computed afresh under the model lock rather than read from the
// panels' cached services, since the questions come from another thread in the web host.
// In-app features call this directly: IModelQueryService resolves to the RPC proxy in the UI.
interface IModelQueries
{
    Result<ModelSummary> GetModel();
    Result<NodeChildren> GetChildren(string nodeName);
    Result<IReadOnlyList<QueryNode>> SearchNodes(string query);
    Result<NodeDependencies> GetDependencies(string nodeName, bool isIncoming);
    Result<PathsResult> FindPaths(string fromName, string toName);
    Result<CycleList> ListCycles();
    Result<IReadOnlyList<RuleResult>> ListRules();
    Result<RuleResult> CheckRule(string fromName, string toName);
}

[Scoped]
sealed class ModelQueries(IModelMgr modelMgr, INodeSearchService nodeSearchService, ModelQueryService host)
    : IModelQueries,
        IDisposable
{
    // Caps that keep an answer small enough for a model's context.
    internal const int MaxChildren = 100;
    internal const int MaxSearchResults = 25;
    internal const int MaxGroups = 50;
    internal const int MaxExamples = 3;
    internal const int MaxPaths = 5;
    internal const int MaxCycles = 50;
    internal const int MaxCycleMembers = 20;
    internal const int MaxViolations = 25;
    internal const int MaxAllowed = 25;
    const int MaxDescriptionLength = 200;

    // Makes this scope's model the one the queries answer on (the latest one opened).
    public void Activate() => host.Add(this);

    public void Dispose() => host.Remove(this);

    public Result<ModelSummary> GetModel()
    {
        using var model = modelMgr.UseModel();
        if (CheckHasModel(model) is Error e)
            return e;
        return new ModelSummary(
            model.Path,
            model.Nodes.Count - 1, // Not the root
            model.Links.Count,
            model.Rules.Count,
            CycleService.Compute(model).Cycles.Count,
            [.. Sorted(model.Root.Children).Take(MaxChildren).Select(ToQueryNode)],
            model.Root.Children.Count
        );
    }

    public Result<NodeChildren> GetChildren(string nodeName)
    {
        using var model = modelMgr.UseModel();
        if (CheckHasModel(model) is Error e)
            return e;
        var node = model.Root;
        if (!string.IsNullOrWhiteSpace(nodeName))
        {
            var found = FindNode(model, nodeName);
            if (found is not Node n)
                return found.Error;
            node = n;
        }
        return new NodeChildren(
            ToQueryNode(node),
            [.. Sorted(node.Children).Take(MaxChildren).Select(ToQueryNode)],
            node.Children.Count
        );
    }

    public Result<IReadOnlyList<QueryNode>> SearchNodes(string query)
    {
        // The search snapshots the nodes under the lock and scores them outside it.
        var results = nodeSearchService.Search(query);
        using var model = modelMgr.UseModel();
        if (CheckHasModel(model) is Error e)
            return e;
        return new Result<IReadOnlyList<QueryNode>>([
            .. results
                .Select(r => model.Nodes.GetValueOrDefault(r.Id))
                .OfType<Node>()
                .Take(MaxSearchResults)
                .Select(ToQueryNode),
        ]);
    }

    public Result<NodeDependencies> GetDependencies(string nodeName, bool isIncoming)
    {
        using var model = modelMgr.UseModel();
        if (CheckHasModel(model) is Error e)
            return e;
        var found = FindNode(model, nodeName);
        if (found is not Node subject)
            return found.Error;

        // Per group (the other side's node at the level where both sides meet), the links per
        // unit pair inside it.
        var subjectAncestors = subject.AncestorsAndSelf().ToHashSet();
        var groups = new Dictionary<Node, Dictionary<(Node, Node), int>>();
        foreach (var link in model.Links.Values)
        {
            var (inside, outside) = isIncoming ? (link.Target, link.Source) : (link.Source, link.Target);
            if (!inside.IsAtOrInside(subject) || outside.IsAtOrInside(subject))
                continue;
            var pair = (UnitGraph.UnitOf(link.Source), UnitGraph.UnitOf(link.Target));
            if (pair.Item1 == pair.Item2)
                continue; // A member of a type using a sibling member
            var group = MeetingLevel(subjectAncestors, outside);
            if (!groups.TryGetValue(group, out var pairs))
                groups[group] = pairs = [];
            pairs[pair] = pairs.GetValueOrDefault(pair) + 1;
        }

        var list = groups
            .Select(g => (Node: g.Key, Pairs: g.Value, LinkCount: g.Value.Values.Sum()))
            .OrderByDescending(g => g.LinkCount)
            .ThenBy(g => g.Node.Name, StringComparer.Ordinal)
            .Take(MaxGroups)
            .Select(g => new DependencyGroup(
                ToQueryNode(g.Node),
                g.LinkCount,
                [
                    .. g
                        .Pairs.OrderByDescending(p => p.Value)
                        .ThenBy(p => p.Key.Item1.Name, StringComparer.Ordinal)
                        .ThenBy(p => p.Key.Item2.Name, StringComparer.Ordinal)
                        .Take(MaxExamples)
                        .Select(p => new UnitDependency(p.Key.Item1.Name, p.Key.Item2.Name, p.Value)),
                ]
            ))
            .ToList();
        return new NodeDependencies(ToQueryNode(subject), isIncoming, list, groups.Count);
    }

    public Result<PathsResult> FindPaths(string fromName, string toName)
    {
        using var model = modelMgr.UseModel();
        if (CheckHasModel(model) is Error e)
            return e;
        var foundFrom = FindNode(model, fromName);
        if (foundFrom is not Node from)
            return foundFrom.Error;
        var foundTo = FindNode(model, toName);
        if (foundTo is not Node to)
            return foundTo.Error;

        var result = PathFinderService.Compute(model, from, to, MaxPaths);
        IReadOnlyList<IReadOnlyList<string>> paths =
        [
            .. result.Paths.Select(p => (IReadOnlyList<string>)[.. p.Hops.Select(h => model.Nodes[h.Id].Name)]),
        ];
        return new PathsResult(result.Status.ToString(), paths);
    }

    public Result<CycleList> ListCycles()
    {
        using var model = modelMgr.UseModel();
        if (CheckHasModel(model) is Error e)
            return e;
        var computed = CycleService.Compute(model);
        List<CycleResult> cycles =
        [
            .. computed.Cycles.Select(c =>
                ToCycleResult(c.ContainerNodeName, [.. c.Members.Select(m => m.NodeName)], "Open", null, true)
            ),
            .. computed.ToFix.Select(r => ToCycleResult(r, "ToFix")),
            .. computed.Accepted.Select(r => ToCycleResult(r, "Accepted")),
        ];
        return new CycleList([.. cycles.Take(MaxCycles)], cycles.Count);
    }

    public Result<IReadOnlyList<RuleResult>> ListRules()
    {
        using var model = modelMgr.UseModel();
        if (CheckHasModel(model) is Error e)
            return e;
        return new Result<IReadOnlyList<RuleResult>>([
            .. RuleService.Compute(model).Reports.Select(r => ToRuleResult(r, isRule: true)),
        ]);
    }

    public Result<RuleResult> CheckRule(string fromName, string toName)
    {
        using var model = modelMgr.UseModel();
        if (CheckHasModel(model) is Error e)
            return e;
        var foundFrom = FindNode(model, fromName);
        if (foundFrom is not Node from)
            return foundFrom.Error;
        var foundTo = FindNode(model, toName);
        if (foundTo is not Node to)
            return foundTo.Error;
        // Two members of one type are the type depending on itself, as nested as a node and its child.
        if (from.IsAtOrInside(to) || to.IsAtOrInside(from) || UnitGraph.UnitOf(from) == UnitGraph.UnitOf(to))
            return new Error("A rule needs two nodes where neither contains the other.");

        var rule = new ArchitectureRule(from.Name, to.Name);
        var report = RuleService.Compute(model, [rule]).Reports.Single();
        return ToRuleResult(report, model.Rules.Contains(rule));
    }

    static Result CheckHasModel(IModel model) =>
        model.Root.Children.Count > 0 ? Result.Ok : new Error("The diagram has no model loaded yet.");

    // A node by its name, or by its title or alias when only one node has it.
    internal static Result<Node> FindNode(IModel model, string name)
    {
        name = name.Trim();
        if (model.Nodes.TryGetValue(NodeId.FromName(name), out var byName) && !byName.IsRoot)
            return byName;
        foreach (var comparison in (StringComparison[])[StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase])
        {
            var matches = model
                .Nodes.Values.Where(n =>
                    !n.IsRoot
                    && (string.Equals(n.LongName, name, comparison) || string.Equals(n.Alias, name, comparison))
                )
                .ToList();
            if (matches.Count == 1)
                return matches[0];
            if (matches.Count > 1)
                return new Error(
                    $"Several nodes are titled \"{name}\"; use one of their names: "
                        + string.Join(", ", matches.Select(n => n.Name).Order(StringComparer.Ordinal).Take(5))
                );
        }
        return new Error($"No node is named \"{name}\"; search for it to find its name.");
    }

    // The other side's node where the two sides meet: the child of their nearest common ancestor
    // on the other side's way up.
    static Node MeetingLevel(IReadOnlySet<Node> subjectAncestors, Node other)
    {
        var node = other;
        while (node.Parent is not null && !subjectAncestors.Contains(node.Parent))
            node = node.Parent;
        return node;
    }

    static IEnumerable<Node> Sorted(IEnumerable<Node> nodes) => nodes.OrderBy(n => n.Name, StringComparer.Ordinal);

    static QueryNode ToQueryNode(Node node) =>
        node.IsRoot
            ? new QueryNode("", "(model)", node.Type.ToString(), node.Children.Count)
            : new QueryNode(
                node.Name,
                node.LongName,
                node.Type.ToString(),
                node.Children.Count,
                Shorten(node.Description),
                node.Alias
            );

    static string? Shorten(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = text.Trim();
        return text.Length <= MaxDescriptionLength ? text : text[..(MaxDescriptionLength - 1)] + "…";
    }

    static CycleResult ToCycleResult(AcceptedCycleReport report, string status) =>
        ToCycleResult(
            report.Accepted.ContainerName,
            report.Accepted.MemberNames,
            report.HasChanged ? "Outgrown" : status,
            report.Accepted.Reason,
            report.StillOccurs
        );

    static CycleResult ToCycleResult(
        string container,
        IReadOnlyList<string> members,
        string status,
        string? reason,
        bool stillOccurs
    ) => new(container, [.. members.Take(MaxCycleMembers)], members.Count, status, reason, stillOccurs);

    static RuleResult ToRuleResult(RuleReport report, bool isRule) =>
        new(
            report.Rule.FromName,
            report.Rule.ToName,
            isRule,
            report.IsResolved,
            report.Violations.Count,
            report.Violations.Sum(v => v.LinkCount),
            [
                .. report
                    .Violations.OrderByDescending(v => v.LinkCount)
                    .Take(MaxViolations)
                    .Select(v => new UnitDependency(v.SourceNodeName, v.TargetNodeName, v.LinkCount)),
            ],
            [
                .. report
                    .Allowed.Take(MaxAllowed)
                    .Select(a => new AllowedDependency(
                        a.Allowed.SourceName,
                        a.Allowed.TargetName,
                        a.Allowed.Reason,
                        a.LinkCount
                    )),
            ],
            report.Allowed.Count
        );
}
