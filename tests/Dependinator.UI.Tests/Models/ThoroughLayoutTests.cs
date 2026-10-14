using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Layout;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

public class ThoroughLayoutTests
{
    const long Budget = 2_000_000;

    [Fact]
    public void Thorough_RandomGraphs_ShouldReadClearlyBetterThanFast()
    {
        var fastScore = 0.0;
        var thoroughScore = 0.0;
        for (var seed = 1; seed <= 15; seed++)
        {
            var graph = RandomGraph(seed);
            fastScore += LayoutQuality.Evaluate(graph, LayeredEngine.RunFast(LayeredEngine.Prepare(graph)!)).Score;
            thoroughScore += LayoutQuality.Evaluate(graph, Thorough(graph)).Score;
        }

        Assert.True(thoroughScore < 0.8 * fastScore, $"Thorough {thoroughScore:0} vs fast {fastScore:0}");
    }

    [Fact]
    public void Thorough_ShouldGiveTheSameResult_HoweverTheRunIsSliced()
    {
        var graph = RandomGraph(12);
        var whole = Thorough(graph);

        foreach (var slice in new long[] { 1, 997, 20_000 })
        {
            var run = new ThoroughLayoutRun(LayeredEngine.Prepare(graph)!, Budget);
            while (!run.IsComplete)
                run.Step(slice);

            Assert.Equal(whole.Boundaries, run.Result.Boundaries);
            for (var edge = 0; edge < graph.Edges.Count; edge++)
                Assert.Equal(whole.Waypoints[edge] ?? [], run.Result.Waypoints[edge] ?? []);
        }
    }

    [Fact]
    public void Thorough_LongLines_ShouldNotPassThroughNodes()
    {
        // A chain with lines skipping one and two columns
        var graph = Graph(4, (0, 1), (1, 2), (2, 3), (0, 2), (0, 3), (1, 3));

        var quality = LayoutQuality.Evaluate(graph, Thorough(graph));

        Assert.Equal(0, quality.LinesThroughNodes);
    }

    [Fact]
    public void Thorough_ShouldAvoidCrossingsThatCanBeAvoided()
    {
        // Two sources each feeding a different target, listed so that a naive order crosses
        var graph = Graph(4, (0, 3), (1, 2), (0, 3), (3, 2));

        var quality = LayoutQuality.Evaluate(graph, Thorough(graph));

        Assert.Equal(0, quality.WeightedCrossings);
    }

    [Fact]
    public void Thorough_TinyBudget_ShouldStillPlaceAllNodesWithoutOverlaps()
    {
        var graph = RandomGraph(20);

        var result = new ThoroughLayoutRun(LayeredEngine.Prepare(graph)!, workBudget: 10).RunToCompletion();

        Assert.All(result.Boundaries, boundary => Assert.NotEqual(Rect.None, boundary));
        for (var first = 0; first < result.Boundaries.Count; first++)
        {
            for (var second = first + 1; second < result.Boundaries.Count; second++)
                Assert.False(Overlaps(result.Boundaries[first], result.Boundaries[second]));
        }
    }

    [Fact]
    public void TryArrange_ShouldRunThoroughly_WhenTheEstimatedWorkFitsTheBudget()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = new ModelNode("Parent", model.Root) { Type = NodeType.Namespace, Boundary = new(0, 0, 1200, 800) };
        model.Root.AddChild(parent);
        model.TryAddNode(parent);
        var a = AddChild(model, parent, "A");
        var b = AddChild(model, parent, "B");
        var c = AddChild(model, parent, "C");
        AddLink(model, a, b);
        AddLink(model, b, c);
        var metrics = new LayoutMetrics(100, 100, 70, 70, 1.0);

        Assert.True(LayeredLayout.TryArrange(parent, metrics, _ => NodeLayout.DefaultSize, Budget, out var isThorough));
        Assert.True(isThorough);
        Assert.True(LayeredLayout.TryArrange(parent, metrics, _ => NodeLayout.DefaultSize, 1, out isThorough));
        Assert.False(isThorough);
    }

    static LayoutResult Thorough(LayoutGraph graph) =>
        new ThoroughLayoutRun(LayeredEngine.Prepare(graph)!, Budget).RunToCompletion();

    // A random graph with cycles, heavy lines, mixed sizes and a few entry nodes
    static LayoutGraph RandomGraph(int seed)
    {
        var random = new Random(seed);
        var count = 6 + seed * 2;
        double[] steps = [60, 100, 140, 180];
        var vertices = Enumerable
            .Range(0, count)
            .Select(index =>
            {
                var side = steps[random.Next(steps.Length)];
                return new LayoutVertex($"N{index:000}", side, side, index % 7 == 0 ? 1 : 0, 0);
            })
            .ToList();
        var edges = new List<LayoutEdge>();
        for (var index = 0; index < count * 3 / 2; index++)
        {
            var source = random.Next(count);
            var target = random.Next(count);
            if (source != target)
                edges.Add(new LayoutEdge(source, target, 1 + random.Next(4), IsHidden: false, IsUserRouted: false));
        }
        return new LayoutGraph(vertices, edges, new LayoutMetrics(100, 100, 70, 70, 1.0), 1.5);
    }

    static LayoutGraph Graph(int count, params (int Source, int Target)[] edges) =>
        new(
            [.. Enumerable.Range(0, count).Select(index => new LayoutVertex($"N{index}", 100, 100, 0, 0))],
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
            1.5
        );

    static bool Overlaps(Rect a, Rect b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    static ModelNode AddChild(IModel model, ModelNode parent, string name)
    {
        var child = new ModelNode(name, parent) { Type = NodeType.ClassType };
        parent.AddChild(child);
        model.TryAddNode(child);
        return child;
    }

    static void AddLink(IModel model, ModelNode source, ModelNode target) =>
        new LineService().AddLinesFromSourceToTarget(model, new Link(source, target));
}
