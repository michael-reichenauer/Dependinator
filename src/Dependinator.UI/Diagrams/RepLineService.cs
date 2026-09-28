using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

// Resolves which lines are rendered at a given zoom. By default every link is drawn as the
// aggregated "funnel" built by LineService: child-to-parent segments up to the source-side top
// sibling under the link's common ancestor, one sibling line between the two top siblings, and
// parent-to-child segments down to the target. The user can split a container's bundles by
// giving it a direct-line depth (Node.LineSplitDepth): the representative walk then descends
// that many levels into the container, on the source side as well as on the target side, as
// far as the nodes actually show their children at this zoom. The dependency explorer's
// subject (Model.LineFocus) is drawn the same way: its links descend to the subject on the
// near side and into the tree rows expanded in the explorer on the far side, and their lines
// are flagged IsFocused. Links are grouped by their (source rep, target rep) pair and drawn as
// one line per pair.
//
// Top-sibling pairs coincide with the sibling chain lines built by LineService (same LineId),
// so those Line objects are reused with their waypoints and descriptions. Deeper pairs cross
// container boundaries ("cousins") and are materialized here as lines rendered inside the
// common ancestor, like direct lines. The chain lines stay in the model as layout and
// dependency-explorer input; chain segments render only when flagged IsActiveRep, which Sync
// sets on the segments below each rep so the funnel still fans out inside the containers.
//
// Sync runs on every tile creation while the model lock is held, so the per-link work is
// kept allocation-free: ancestor paths go into two reused buffers and the common ancestor
// comes from suffix comparison instead of set-based LCA lookups.
static class RepLineService
{
    record SyncState(double Zoom, int StructureVersion, List<KeyValuePair<Node, bool>> ExpansionStates);

    sealed class LinkGroup
    {
        public List<Link> Links { get; } = [];
        public bool IsFocused { get; set; }
    }

    // Last synced state per model: tiles are re-created far more often than the reps actually
    // change (panning, post-change re-renders, and especially zoom animations, where the zoom
    // differs every frame but no node crosses an expansion threshold). The reps depend on the
    // model structure (which includes the split and focus state, see Model.BumpStructureVersion) and on
    // the expansion state of exactly the nodes the last resolution visited, so the sync can be
    // skipped when the structure version matches and every one of those nodes still has the
    // same expansion state at the new zoom. Node property changes (e.g. a resize adjusting
    // ContainerZoom) do not bump the version; reps can be momentarily stale in that edge case
    // until the next structure change or expansion flip.
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
            line.IsFocused = false;
        }

        var (groups, isChildrenShownCache) = GroupLinksByRepPair(model, zoom);
        foreach (var (pair, group) in groups)
        {
            var id = pair.IsInheritance
                ? LineId.FromInheritance(pair.Source.Name, pair.Target.Name)
                : LineId.From(pair.Source.Name, pair.Target.Name);

            if (model.Lines.TryGetValue(id, out var line))
            {
                line.IsActiveRep = true;
                line.IsFocused = group.IsFocused;
                if (line.IsCousin)
                    ReconcileLinks(line, group.Links);
            }
            else
            {
                AddCousinLine(model, id, pair, group);
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

    static (Dictionary<RepPair, LinkGroup> Groups, Dictionary<Node, bool> IsChildrenShownCache) GroupLinksByRepPair(
        IModel model,
        double zoom
    )
    {
        var isChildrenShownCache = new Dictionary<Node, bool>();
        var groups = new Dictionary<RepPair, LinkGroup>();
        var sourcePath = new List<Node>(32);
        var targetPath = new List<Node>(32);
        var focus = model.LineFocus;

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

            // A focus link descends to the subject on the near side (and to a line subject's far
            // end on the far side), and into the explorer's expanded rows on the far side.
            var isFocusLink = focus is not null && IsFocusLink(focus, link, sourcePath, targetPath);
            Node? sourcePin = null;
            Node? targetPin = null;
            HashSet<Node>? sourceExpanded = null;
            HashSet<Node>? targetExpanded = null;
            if (isFocusLink)
            {
                if (focus!.IsReferences)
                {
                    targetPin = focus.NearNode;
                    sourcePin = focus.FarNode;
                    sourceExpanded = focus.ExpandedFarNodes;
                }
                else
                {
                    sourcePin = focus.NearNode;
                    targetPin = focus.FarNode;
                    targetExpanded = focus.ExpandedFarNodes;
                }
            }

            int sourceRepIndex = GetRepresentativeIndex(
                sourcePath,
                common,
                zoom,
                isChildrenShownCache,
                sourcePin,
                sourceExpanded
            );
            int targetRepIndex = GetRepresentativeIndex(
                targetPath,
                common,
                zoom,
                isChildrenShownCache,
                targetPin,
                targetExpanded
            );

            // The funnel segments below the reps render whether or not the reps differ: a link
            // from a node to its own ancestor (or from a parent to its own child) has equal reps
            // and is drawn entirely by its chain segments.
            ActivateChainSegments(link, sourcePath, sourceRepIndex, targetPath, targetRepIndex);

            var repSource = sourcePath[sourceRepIndex];
            var repTarget = targetPath[targetRepIndex];
            if (repSource == repTarget)
                continue;

            // Same rule as LineService.AddDirectLine: inheritance styling only where the line
            // touches the real inheritance endpoints.
            var isInheritance = link.IsInheritance && (repSource == link.Source || repTarget == link.Target);

            var pair = new RepPair(repSource, repTarget, isInheritance);
            if (!groups.TryGetValue(pair, out var group))
            {
                group = new LinkGroup();
                groups[pair] = group;
            }
            group.Links.Add(link);
            group.IsFocused |= isFocusLink;
        }

        return (groups, isChildrenShownCache);
    }

    // A focus link is one of the subject line's links or, for a node subject, a link whose
    // near endpoint lies at or below the subject node.
    static bool IsFocusLink(LineFocus focus, Link link, List<Node> sourcePath, List<Node> targetPath)
    {
        if (focus.Links is not null)
            return focus.Links.Contains(link);

        var nearPath = focus.IsReferences ? targetPath : sourcePath;
        return IndexOf(nearPath, focus.NearNode) != int.MaxValue;
    }

    static void FillAncestorsAndSelf(Node node, List<Node> path)
    {
        path.Clear();
        for (Node? current = node; current != null; current = current.Parent)
            path.Add(current);
    }

    // Walks from the top sibling under the common ancestor (index count-common-1) toward the
    // endpoint (index 0). The walk descends into a node only while it has depth budget left,
    // or while the node lies above the focus pin or is a focus-expanded node, and the node
    // shows its children at this zoom; with no split depth and no focus it stops at once, which
    // is the aggregated funnel. Pass-through nodes are transparent: they have no chrome of
    // their own, so they neither stop the walk nor consume a level. The depth and focus are
    // checked before the expansion state so nodes that cannot affect the outcome never enter
    // the memo's expansion cache.
    static int GetRepresentativeIndex(
        List<Node> path,
        int commonAncestors,
        double zoom,
        Dictionary<Node, bool> cache,
        Node? pinNode,
        HashSet<Node>? expandedNodes
    )
    {
        int index = path.Count - commonAncestors - 1;
        int budget = GetInheritedDepth(path, index);
        int pinIndex = pinNode is null ? int.MaxValue : IndexOf(path, pinNode); // MaxValue: no pin on this path
        while (index > 0)
        {
            var node = path[index];
            if (node.IsPassThrough)
            {
                index--;
                continue;
            }

            budget = Math.Max(budget, node.LineSplitDepth);
            var isFocusDescent = index > pinIndex || (expandedNodes is not null && expandedNodes.Contains(node));
            if ((budget == 0 && !isFocusDescent) || !IsChildrenShown(node, zoom, cache))
                break;

            if (budget > 0)
                budget--;
            index--;
        }

        return index;
    }

    // The depth budget the ancestors above the top sibling grant it: an ancestor's split depth
    // counts from that ancestor, so the levels already spent down to the top sibling are
    // subtracted (pass-through nodes are not levels). This is what makes a split depth apply to
    // the links inside the container too, whose top siblings lie below it.
    static int GetInheritedDepth(List<Node> path, int topIndex)
    {
        int budget = 0;
        int levels = 0;
        for (int i = topIndex + 1; i < path.Count; i++)
        {
            if (!path[i - 1].IsPassThrough)
                levels++;
            budget = Math.Max(budget, path[i].LineSplitDepth - levels);
        }

        return budget;
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

    // Activates the link's funnel segments strictly below its representatives: the
    // child-to-parent segments on the source path up to the source rep and the parent-to-child
    // segments on the target path down from the target rep. Segments at or above a rep are
    // replaced by the rep pair line. The link's own line list is scanned (its chain segments
    // plus any cousin lines it has been part of): short, and no allocation.
    static void ActivateChainSegments(
        Link link,
        List<Node> sourcePath,
        int sourceRepIndex,
        List<Node> targetPath,
        int targetRepIndex
    )
    {
        if (sourceRepIndex == 0 && targetRepIndex == 0)
            return;

        foreach (var line in link.Lines)
        {
            if (line.IsActiveRep || line.IsDirect || line.IsCousin)
                continue;
            if (line.Source.Parent == line.Target)
                line.IsActiveRep = IndexOf(sourcePath, line.Source) < sourceRepIndex;
            else if (line.Target.Parent == line.Source)
                line.IsActiveRep = IndexOf(targetPath, line.Target) < targetRepIndex;
        }
    }

    static int IndexOf(List<Node> path, Node node)
    {
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i] == node)
                return i;
        }
        return int.MaxValue;
    }

    static void AddCousinLine(IModel model, LineId id, RepPair pair, LinkGroup group)
    {
        // The same parent-LCA rule as for links yields the common ancestor the reps were
        // resolved under, also when one rep is an ancestor of the other endpoint or when the
        // target rep lies deep inside its container.
        var renderAncestor = pair.Source.Parent.LowestCommonAncestor(pair.Target.Parent);
        var line = new Line(pair.Source, pair.Target, id: id, isInheritance: pair.IsInheritance)
        {
            RenderAncestor = renderAncestor,
            IsActiveRep = true,
            IsFocused = group.IsFocused,
        };

        renderAncestor.AddDirectLine(line);
        model.TryAddLine(line);

        foreach (var link in group.Links)
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
    // their count is bounded by the distinct rep pairs of the zoom levels, split depths and
    // focus states visited, and pruning caused heavy allocation churn when tiles at different zoom levels
    // alternated (each flip re-created and re-destroyed every deep cousin line).
    // Model.RemoveLink removes them once their last link goes away, like any other line.
}
