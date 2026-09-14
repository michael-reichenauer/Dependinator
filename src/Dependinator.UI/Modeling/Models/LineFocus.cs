namespace Dependinator.UI.Modeling.Models;

// The dependency explorer's subject while its lines are shown in the diagram (see
// DependenciesService and RepLineService): the subject's links are drawn from/to the subject
// itself instead of merging into their containers' bundles, and split on the far side into
// the containers the user has expanded in the explorer tree. Transient view state: Model.Clear
// drops it, and every change bumps Model.StructureVersion.
class LineFocus
{
    LineFocus(bool isReferences, Node nearNode, Node? farNode, HashSet<Link>? links)
    {
        IsReferences = isReferences;
        NearNode = nearNode;
        FarNode = farNode;
        Links = links;
    }

    // All links from (dependencies) or to (references) the node and its descendants.
    public static LineFocus ForNode(Node node, bool isReferences) => new(isReferences, node, null, null);

    // The links of one line; the line's ends pin both sides.
    public static LineFocus ForLine(Line line, bool isReferences) =>
        isReferences
            ? new(true, line.Target, line.Source, [.. line.Links])
            : new(false, line.Source, line.Target, [.. line.Links]);

    // References: the near side is the link target (the subject is being used) and the far
    // side the source; dependencies: the other way around.
    public bool IsReferences { get; }

    // The subject's end: the representative walk descends at least down to it (as far as it
    // is visible), so the subject's lines leave from the subject itself.
    public Node NearNode { get; }

    // A line subject's far endpoint: the far side descends at least down to it. Null for a
    // node subject, whose far side starts at the aggregated level.
    public Node? FarNode { get; }

    // A line subject's links; null means every link under NearNode in the focus direction.
    public HashSet<Link>? Links { get; }

    // Far-side containers expanded in the explorer tree; the walk descends one level into each.
    public HashSet<Node> ExpandedFarNodes { get; } = [];

    // True when both resolve the same lines: same subject ends and direction, and the same
    // expanded far nodes. A line subject is identified by its ends, so its link set is not
    // compared.
    public bool IsSameAs(LineFocus other) =>
        IsReferences == other.IsReferences
        && NearNode == other.NearNode
        && FarNode == other.FarNode
        && (Links is null) == (other.Links is null)
        && ExpandedFarNodes.SetEquals(other.ExpandedFarNodes);
}
