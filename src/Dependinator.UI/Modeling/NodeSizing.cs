using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling;

// The size of a child node in its parent's automatic layout, so a diagram shows what weighs
// most: much code and coordinating many siblings make a node big, small utilities stay small.
// Sizes are relative to the siblings (each container is read on its own) and come in a few
// clearly different steps, 40 apart, so centered nodes and their line anchors stay on the
// 20-unit snap grid and lines between equally centered nodes are straight. The size does not
// depend on how thoroughly the layout is computed, so refining a layout only moves nodes.
static class NodeSizing
{
    public static readonly double[] SideSteps = [60, 100, 140, 180];
    public const double DefaultSide = 100;

    // A node's score is the log2 of its code mass relative to the siblings' median, plus a
    // damped log2 of its centrality relative to theirs; the side doubles every 4 score units
    // (16 times the median code is the largest step, 1/16th the smallest).
    const double MaxMassScore = 4;
    const double CentralityFactor = 0.5;
    const double MinCentralityScore = -0.5;
    const double MaxCentralityScore = 1.5;
    const double ScorePerSideDoubling = 4;

    // The side of each given child (the caller passes the children that have a node size of
    // their own: no members, zones, notes or pass-through nodes).
    public static Dictionary<Node, double> ComputeSides(IReadOnlyList<Node> children, Node parent)
    {
        if (children.Count < 2)
            return children.ToDictionary(child => child, _ => DefaultSide);

        var memo = new Dictionary<Node, double?>();
        var masses = children.ToDictionary(child => child, child => CodeMass(child, memo));
        var centralities = children.ToDictionary(child => child, child => Centrality(child, parent));

        var knownMasses = masses.Values.OfType<double>().ToList();
        var medianMass = knownMasses.Count > 0 ? Median(knownMasses) : 0;
        var medianCentrality = Median(centralities.Values.ToList());

        return children.ToDictionary(
            child => child,
            child =>
            {
                var massScore =
                    masses[child] is { } mass && medianMass > 0
                        ? Math.Clamp(Math.Log2(mass / medianMass), -MaxMassScore, MaxMassScore)
                        : 0;
                var centralityScore = Math.Clamp(
                    CentralityFactor * Math.Log2((centralities[child] + 1) / (medianCentrality + 1)),
                    MinCentralityScore,
                    MaxCentralityScore
                );
                return QuantizeSide(DefaultSide * Math.Pow(2, (massScore + centralityScore) / ScorePerSideDoubling));
            }
        );
    }

    // The amount of code in the node: the lines of its declaration (a C# type, a Bicep
    // declaration) or the sum over its children (a namespace, an assembly, a Bicep module whose
    // declaration is short but whose resources are not), whichever is more, so nested types
    // are not counted twice. A parsed node without source (an external type or member) counts
    // as one line. Null when unknown: a manually drawn node has no code.
    internal static double? CodeMass(Node node, Dictionary<Node, double?> memo)
    {
        if (memo.TryGetValue(node, out var known))
            return known;

        double? mass = null;
        if (!node.IsManual)
        {
            var childMass = 0.0;
            foreach (var child in node.Children)
            {
                if (CodeMass(child, memo) is { } childValue)
                    childMass += childValue;
            }

            var spanLines = node.FileSpan is { } span ? span.EndLine - span.StartLine + 1 : 1;
            mass = Math.Max(Math.Max(1, spanLines), childMass);
        }

        memo[node] = mass;
        return mass;
    }

    // How strongly the node takes part in its container's dependency flow: its sibling lines
    // and the lines crossing the container's edge, heavy lines damped logarithmically. Outgoing
    // dependencies and incoming references from outside count fully, the others half, so a
    // coordinator grows more than a widely used small utility.
    internal static double Centrality(Node child, Node parent)
    {
        var centrality = 0.0;
        foreach (var line in child.SourceLines)
        {
            if (!IsFlowLine(line))
                continue;
            if (line.Target == parent)
                centrality += Weight(line) / 2; // Out of the container
            else if (line.Target.Parent == parent)
                centrality += Weight(line); // To a sibling
        }

        foreach (var line in child.TargetLines)
        {
            if (!IsFlowLine(line))
                continue;
            if (line.Source == parent)
                centrality += Weight(line); // Into the container
            else if (line.Source.Parent == parent)
                centrality += Weight(line) / 2; // From a sibling
        }

        return centrality;
    }

    // The lines the layered layout arranges by (see LayeredLayout.BuildGraph).
    static bool IsFlowLine(Line line) => !line.IsDirect && !line.IsCousin && !line.IsEmpty;

    static double Weight(Line line) => 1 + Math.Log2(Math.Max(1, line.Links.Count));

    // The size step nearest to the side, compared in log space (ratios, not differences).
    internal static double QuantizeSide(double side) => SideSteps.MinBy(step => Math.Abs(Math.Log(side / step)));

    static double Median(List<double> values)
    {
        if (values.Count == 0)
            return 0;
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
    }
}
