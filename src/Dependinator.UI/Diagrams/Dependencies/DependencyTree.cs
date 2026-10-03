using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams.Dependencies;

// One indirect dependency (or reference) of the explorer's subject: the far-side chain of
// containers from just below the common ancestor down to the reached node, and the hop count.
readonly record struct IndirectChain(IReadOnlyList<Node> FarChain, int Hops);

// Builds the explorer's tree. The direct part follows the subject's lines: each level lists the
// far nodes the lines lead to, and expanding a row continues along the lines from that node, so
// the levels are the far-side funnel (top containers first, their children below) that expanding
// a row splits in the diagram. With indirect ones included, the nodes the subject reaches
// through other nodes are merged into the same containers, with their hop counts.
static class DependencyTree
{
    public static IReadOnlyList<TreeItem> ForNode(IModel model, Node node, TreeType treeType, bool includeIndirect)
    {
        var isReferences = treeType is TreeType.References;

        // Only lines carrying a link that really ends at the node or inside it (other links just
        // pass by on their way to some other node).
        var lines = (isReferences ? node.TargetLines : node.SourceLines)
            .Where(line =>
                line.Links.Any(link =>
                {
                    var endpoint = isReferences ? link.Target : link.Source;
                    return endpoint == node || endpoint.Ancestors().Contains(node);
                })
            )
            .ToList();
        var rootLinks = lines.SelectMany(l => l.Links).ToHashSet();
        var indirect = includeIndirect ? GetIndirectChains(model, node, isReferences) : [];
        return Level(lines, indirect, rootLinks, isReferences);
    }

    public static IReadOnlyList<TreeItem> ForLine(Line line, TreeType treeType) =>
        Level([line], [], [.. line.Links], treeType is TreeType.References);

    // The nodes reached through other nodes (two hops or more; the direct ones come from the
    // lines), each with the same far-side chain the lines would have: from just below the
    // common ancestor of the subject and the node down to the node.
    internal static IReadOnlyList<IndirectChain> GetIndirectChains(IModel model, Node subject, bool isReferences)
    {
        var chains = new List<IndirectChain>();
        foreach (var (unit, hops) in UnitGraph.Build(model).Reach(subject, isReferences))
        {
            if (hops < 2 || unit.Parent is null || subject.Ancestors().Contains(unit))
                continue;
            var common = subject.Parent.LowestCommonAncestor(unit.Parent);
            var chain = unit.AncestorsAndSelf().TakeWhile(n => n != common).Reverse().ToList();
            if (chain.Count > 0)
                chains.Add(new IndirectChain(chain, hops));
        }
        return chains;
    }

    static IReadOnlyList<TreeItem> Level(
        IReadOnlyList<Line> lines,
        IReadOnlyList<IndirectChain> indirect,
        HashSet<Link> rootLinks,
        bool isReferences
    )
    {
        var items = new List<TreeItem>();
        var indirectByHead = new Dictionary<Node, List<IndirectChain>>();
        foreach (var chain in indirect)
        {
            if (!indirectByHead.TryGetValue(chain.FarChain[0], out var list))
                indirectByHead[chain.FarChain[0]] = list = [];
            list.Add(chain);
        }

        foreach (var (farNode, farLines) in ResolveFarGroups(lines, rootLinks, isReferences))
        {
            var groupLinks = farLines.SelectMany(l => l.Links).Where(rootLinks.Contains).ToHashSet();
            var nextLines = NextLines(farNode, groupLinks, isReferences);
            indirectByHead.Remove(farNode, out var chains);
            var tails = Tails(chains);

            // Children are created lazily on first expand, after the model lock has been released;
            // the captured lines/nodes may be stale if the model has been re-parsed since.
            GetTreeItemChildren? getChildren =
                nextLines.Count > 0 || tails.Count > 0
                    ? () => [.. Level(nextLines, tails, groupLinks, isReferences)]
                    : null;
            items.Add(new TreeItem(farNode, groupLinks.Count, hopCount: 0, getChildren));
        }

        // Containers and nodes reached only indirectly follow the direct ones, by name.
        foreach (var (head, chains) in indirectByHead.OrderBy(kv => kv.Key.ShortName, StringComparer.OrdinalIgnoreCase))
        {
            var tails = Tails(chains);
            var hops = chains.Where(c => c.FarChain.Count == 1).Select(c => c.Hops).DefaultIfEmpty(0).Min();
            GetTreeItemChildren? getChildren =
                tails.Count > 0 ? () => [.. Level([], tails, rootLinks, isReferences)] : null;
            items.Add(new TreeItem(head, 0, hops, getChildren));
        }

        return items;
    }

    static List<IndirectChain> Tails(List<IndirectChain>? chains) =>
        chains is null
            ? []
            : chains
                .Where(c => c.FarChain.Count > 1)
                .Select(c => new IndirectChain([.. c.FarChain.Skip(1)], c.Hops))
                .ToList();

    static List<Line> NextLines(Node farNode, HashSet<Link> links, bool isReferences) =>
        (isReferences ? farNode.TargetLines : farNode.SourceLines).Where(l => l.Links.Any(links.Contains)).ToList();

    // The far nodes this level's lines lead to, each with the lines leading there. A usage and
    // an inheritance segment to the same node are two lines in the model but one dependency to
    // the user, so they share a row. A line to the near node's own parent adds no information:
    // the chain continues from the parent, so the row shows the container the links go on to.
    static List<(Node Far, List<Line> Lines)> ResolveFarGroups(
        IEnumerable<Line> lines,
        HashSet<Link> rootLinks,
        bool isReferences
    )
    {
        var order = new List<Node>();
        var groups = new Dictionary<Node, List<Line>>();
        Collect(lines);
        return order.Select(far => (far, groups[far])).ToList();

        void Collect(IEnumerable<Line> candidates)
        {
            foreach (var line in candidates)
            {
                var (farNode, nearNode) = isReferences ? (line.Source, line.Target) : (line.Target, line.Source);
                if (nearNode.Parent == farNode)
                {
                    Collect(NextLines(farNode, rootLinks, isReferences));
                    continue;
                }
                if (!groups.TryGetValue(farNode, out var list))
                {
                    groups[farNode] = list = [];
                    order.Add(farNode);
                }
                if (!list.Contains(line))
                    list.Add(line);
            }
        }
    }
}
