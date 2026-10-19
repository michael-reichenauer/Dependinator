using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Layout;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Models;

public class LayoutQualityTests
{
    // A and B on the left above each other, C and D on the right
    static readonly Rect A = new(0, 0, 100, 100);
    static readonly Rect B = new(0, 200, 100, 100);
    static readonly Rect C = new(300, 0, 100, 100);
    static readonly Rect D = new(300, 200, 100, 100);

    [Fact]
    public void Evaluate_TwoCrossingLines_ShouldCountOneCrossing()
    {
        var quality = Evaluate([A, B, C, D], [(0, 3), (1, 2)]);

        Assert.Equal(1, quality.WeightedCrossings);
    }

    [Fact]
    public void Evaluate_ParallelLines_ShouldCountNoCrossing()
    {
        var quality = Evaluate([A, B, C, D], [(0, 2), (1, 3)]);

        Assert.Equal(0, quality.WeightedCrossings);
    }

    [Fact]
    public void Evaluate_LinesFromTheSameNode_ShouldNotCountAsCrossing()
    {
        var quality = Evaluate([A, B, C, D], [(0, 2), (0, 3)]);

        Assert.Equal(0, quality.WeightedCrossings);
    }

    [Fact]
    public void Evaluate_LineThroughANode_ShouldCountIt_UnlessRoutedAround()
    {
        Rect[] boundaries = [new(0, 0, 100, 100), new(300, 0, 100, 100), new(600, 0, 100, 100)];

        var straight = Evaluate(boundaries, [(0, 2)]);
        var routed = Evaluate(
            boundaries,
            [(0, 2)],
            [
                [new Pos(350, 150)],
            ]
        );

        Assert.Equal(1, straight.LinesThroughNodes);
        Assert.Equal(0, routed.LinesThroughNodes);
        Assert.Equal(1, routed.Bends);
        Assert.True(routed.Score < straight.Score);
    }

    static LayoutQuality Evaluate(
        Rect[] boundaries,
        (int Source, int Target)[] edges,
        IReadOnlyList<Pos>?[]? waypoints = null
    )
    {
        var graph = new LayoutGraph(
            [.. boundaries.Select((rect, index) => new LayoutVertex($"N{index}", rect.Width, rect.Height, 0, 0))],
            [
                .. edges.Select(edge => new LayoutEdge(
                    edge.Source,
                    edge.Target,
                    1,
                    IsHidden: false,
                    IsUserRouted: false
                )),
            ],
            new LayoutMetrics(100, 100, 70, 70, 1.0),
            1.0
        );
        var result = new LayoutResult(boundaries, waypoints ?? new IReadOnlyList<Pos>?[edges.Length]);
        return LayoutQuality.Evaluate(graph, result);
    }
}
