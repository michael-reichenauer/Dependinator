using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

// Resolves which lines are rendered at a given zoom: each link is drawn as one line from the
// "deepest visible representative" of its source straight to the target-side TOP sibling
// under the link's common ancestor (the target container). The source representative walks
// down from the source-side top sibling toward the endpoint, descending while nodes show
// their children, and stops at the first icon/member node (or the endpoint itself). The
// target side never descends: inside the target container the link fans out via the ordinary
// parent-to-child chain segments (rendered ungated, see SvgService.RenderNodeLines), exactly
// like the original aggregation. This keeps crossing-line counts at one per source child and
// target container instead of source-child x target-child pairs.
//
// Top-level rep pairs coincide with the aggregated sibling chain lines built by LineService
// (same LineId), so those Line objects are reused with their waypoints and descriptions.
// Deeper pairs cross container boundaries ("cousins") and are materialized here as lines
// rendered inside the common ancestor, like direct lines. The chain lines stay in the model
// as layout and dependency-explorer input; sibling and child-to-parent chain segments render
// only when flagged IsActiveRep.
//
// Sync runs on every tile creation while the model lock is held, so the per-link work is
// kept allocation-free: ancestor paths go into two reused buffers and the common ancestor
// comes from suffix comparison instead of set-based LCA lookups.
static class RepLineService
{
    record SyncState(double Zoom, int StructureVersion, List<KeyValuePair<Node, bool>> ExpansionStates);

    // Last synced state per model: tiles are re-created far more often than the reps actually
    // change (panning, post-change re-renders, and especially zoom animations, where the zoom
    // differs every frame but no node crosses an expansion threshold). The reps depend on the
    // model structure and on the expansion state of exactly the nodes the last resolution
    // visited, so the sync can be skipped when the structure version matches and every one of
    // those nodes still has the same expansion state at the new zoom. Node property changes
    // (e.g. a resize adjusting ContainerZoom) do not bump the version; reps can be momentarily
    // stale in that edge case until the next structure change or expansion flip.
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IModel, SyncState> lastSync = new();

    public static void Sync(IModel model, double zoom)
    {
        if (lastSync.TryGetValue(model, out var state) && state.StructureVersion == model.StructureVersion)
        {
            if (state.Zoom == zoom || AllExpansionStatesUnchanged(state.ExpansionStates, zoom))
                return;
        }

        foreach (var line in model.Lines.Values)
        {
            line.IsActiveRep = false;
        }

        var (groups, isChildrenShownCache) = GroupLinksByRepPair(model, zoom);
        foreach (var (pair, links) in groups)
        {
            var id = pair.IsInheritance
                ? LineId.FromInheritance(pair.Source.Name, pair.Target.Name)
                : LineId.From(pair.Source.Name, pair.Target.Name);

            if (model.Lines.TryGetValue(id, out var line))
            {
                line.IsActiveRep = true;
                if (line.IsCousin)
                    ReconcileLinks(line, links);
            }
            else
            {
                AddCousinLine(model, id, pair, links);
            }
        }

        lastSync.AddOrUpdate(model, new SyncState(zoom, model.StructureVersion, [.. isChildrenShownCache]));
    }

    // A rep walk descends only through nodes it saw as expanded and stops at the first
    // collapsed one, so a walk can only change when a node visited last time flips state;
    // nodes never visited cannot affect the outcome without such a flip above them.
    static bool AllExpansionStatesUnchanged(List<KeyValuePair<Node, bool>> expansionStates, double zoom)
    {
        foreach (var (node, wasShown) in expansionStates)
        {
            if (NodeViewPolicy.IsChildrenShown(node, zoom) != wasShown)
                return false;
        }
        return true;
    }

    readonly record struct RepPair(Node Source, Node Target, bool IsInheritance);

    static (Dictionary<RepPair, List<Link>> Groups, Dictionary<Node, bool> IsChildrenShownCache) GroupLinksByRepPair(
        IModel model,
        double zoom
    )
    {
        var isChildrenShownCache = new Dictionary<Node, bool>();
        var groups = new Dictionary<RepPair, List<Link>>();
        var sourcePath = new List<Node>(32);
        var targetPath = new List<Node>(32);

        foreach (var link in model.Links.Values)
        {
            FillAncestorsAndSelf(link.Source, sourcePath);
            FillAncestorsAndSelf(link.Target, targetPath);

            // Number of common trailing (root-side) nodes of the two PARENT chains; the common
            // ancestor is the deepest of these. Using the parent chains (skip index 0) mirrors
            // LineService.GetCommonAncestor, so a link between a node and one of its ancestors
            // still yields a non-empty path on both sides.
            int maxCommon = Math.Min(sourcePath.Count, targetPath.Count) - 1;
            int common = 0;
            while (common < maxCommon && sourcePath[^(common + 1)] == targetPath[^(common + 1)])
                common++;

            var repSource = GetRepresentative(sourcePath, common, zoom, isChildrenShownCache);
            var repTarget = targetPath[targetPath.Count - common - 1]; // Target top sibling; never descends
            if (repSource == repTarget)
                continue;

            // Same rule as LineService.AddDirectLine: inheritance styling only where the line
            // touches the real inheritance endpoints.
            var isInheritance = link.IsInheritance && (repSource == link.Source || repTarget == link.Target);

            var pair = new RepPair(repSource, repTarget, isInheritance);
            if (!groups.TryGetValue(pair, out var links))
            {
                links = [];
                groups[pair] = links;
            }
            links.Add(link);
        }

        return (groups, isChildrenShownCache);
    }

    static void FillAncestorsAndSelf(Node node, List<Node> path)
    {
        path.Clear();
        for (Node? current = node; current != null; current = current.Parent)
            path.Add(current);
    }

    // Walks down from just below the common ancestor (path index count-common-1) toward the
    // endpoint (index 0), descending while nodes show their children.
    static Node GetRepresentative(List<Node> path, int commonAncestors, double zoom, Dictionary<Node, bool> cache)
    {
        var representative = path[0];
        for (int i = path.Count - commonAncestors - 1; i >= 0; i--)
        {
            representative = path[i];
            if (i == 0 || !IsChildrenShown(path[i], zoom, cache))
                break;
        }

        return representative;
    }

    static bool IsChildrenShown(Node node, double zoom, Dictionary<Node, bool> cache)
    {
        if (!cache.TryGetValue(node, out var isShown))
        {
            isShown = NodeViewPolicy.IsChildrenShown(node, zoom);
            cache[node] = isShown;
        }
        return isShown;
    }

    static void AddCousinLine(IModel model, LineId id, RepPair pair, List<Link> links)
    {
        // The same parent-LCA rule as for links yields the common ancestor the reps were
        // resolved under, also when one rep is an ancestor of the other endpoint.
        var renderAncestor = pair.Source.Parent.LowestCommonAncestor(pair.Target.Parent);
        var line = new Line(pair.Source, pair.Target, id: id, isInheritance: pair.IsInheritance)
        {
            RenderAncestor = renderAncestor,
            IsActiveRep = true,
        };

        renderAncestor.AddDirectLine(line);
        model.TryAddLine(line);

        foreach (var link in links)
        {
            line.Add(link);
            link.AddLine(line);
        }

        UpdateHidden(line);
    }

    // Brings a reactivated cousin line's link set up to date. The fast path (same links as
    // before, the overwhelmingly common case when zoom levels alternate) avoids allocations.
    static void ReconcileLinks(Line line, List<Link> links)
    {
        if (line.Links.Count == links.Count && links.All(line.Contains))
        {
            UpdateHidden(line);
            return;
        }

        var wanted = links.ToHashSet();
        foreach (var stale in line.Links.Where(link => !wanted.Contains(link)).ToList())
        {
            line.Remove(stale);
            stale.RemoveLine(line);
        }

        foreach (var link in links)
        {
            line.Add(link);
            link.AddLine(line);
        }

        UpdateHidden(line);
    }

    // Same visibility rule as ModelService.CheckLineVisibility.
    static void UpdateHidden(Line line) =>
        line.IsHidden = line.Links.All(link => link.Source.IsHidden || link.Target.IsHidden);

    // Inactive cousin lines are deliberately NOT pruned: they are invisible (!IsActiveRep),
    // their count is bounded by the distinct rep pairs of the zoom levels visited, and
    // pruning caused heavy allocation churn when tiles at different zoom levels alternated
    // (each flip re-created and re-destroyed every deep cousin line). Model.RemoveLink
    // removes them once their last link goes away, like any other line.
}
