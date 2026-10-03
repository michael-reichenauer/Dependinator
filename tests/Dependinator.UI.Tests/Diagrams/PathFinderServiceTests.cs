using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The path finder: shortest chains of dependencies from one node to another, where members count
// for their type, container endpoints stand for everything inside them, every equally short chain
// is listed, and nested or unconnected endpoints are reported as such.
public class PathFinderServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

    static Node AddNode(IModel model, string name, Node parent, NodeType type = NodeType.ClassType)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100), Type = type };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static void AddLink(IModel model, Node source, Node target)
    {
        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
    }

    static string[] Names(DependencyPath path) => path.Hops.Select(h => h.Name).ToArray();

    [Fact]
    public void Compute_ShouldFindShortestChain_AndItsLines()
    {
        using var model = modelMgr.UseModel();
        var a = AddNode(model, "A", model.Root);
        var b = AddNode(model, "B", model.Root);
        var c = AddNode(model, "C", model.Root);
        var d = AddNode(model, "D", model.Root);
        AddLink(model, a, b);
        AddLink(model, b, c);
        AddLink(model, a, d);
        AddLink(model, d, b); // A→D→B→C is longer than A→B→C

        var result = PathFinderService.Compute(model, a, c);

        Assert.Equal(PathStatus.Found, result.Status);
        var path = Assert.Single(result.Paths);
        Assert.Equal(["A", "B", "C"], Names(path));
        Assert.Equal(2, path.HopCount);
        Assert.Contains(LineId.From(a.Name, b.Name), path.LineIds);
        Assert.Contains(LineId.From(b.Name, c.Name), path.LineIds);
        Assert.DoesNotContain(LineId.From(a.Name, d.Name), path.LineIds);
    }

    [Fact]
    public void Compute_ShouldListEveryEquallyShortChain_InNameOrder()
    {
        using var model = modelMgr.UseModel();
        var a = AddNode(model, "A", model.Root);
        var b = AddNode(model, "B", model.Root);
        var c = AddNode(model, "C", model.Root);
        var d = AddNode(model, "D", model.Root);
        AddLink(model, a, c);
        AddLink(model, c, d);
        AddLink(model, a, b);
        AddLink(model, b, d);

        var result = PathFinderService.Compute(model, a, d);

        Assert.Equal(2, result.Paths.Count);
        Assert.Equal(["A", "B", "D"], Names(result.Paths[0]));
        Assert.Equal(["A", "C", "D"], Names(result.Paths[1]));
    }

    [Fact]
    public void Compute_ShouldCountMemberLinksForTheirTypes()
    {
        using var model = modelMgr.UseModel();
        var a = AddNode(model, "A", model.Root);
        var b = AddNode(model, "B", model.Root);
        var c = AddNode(model, "C", model.Root);
        var aMethod = AddNode(model, "A.Run()", a, NodeType.MethodMember);
        var bMethod = AddNode(model, "B.Help()", b, NodeType.MethodMember);
        var cField = AddNode(model, "C.value", c, NodeType.FieldMember);
        AddLink(model, aMethod, bMethod);
        AddLink(model, bMethod, cField);

        var result = PathFinderService.Compute(model, aMethod, c);

        var path = Assert.Single(result.Paths);
        Assert.Equal(["A", "B", "C"], Names(path));
    }

    [Fact]
    public void Compute_ShouldStartAnywhereInsideAContainer_AndEndAnywhereInsideTheOther()
    {
        using var model = modelMgr.UseModel();
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        var core = AddNode(model, "Core", model.Root, NodeType.Assembly);
        var shared = AddNode(model, "Shared", model.Root, NodeType.Assembly);
        var view = AddNode(model, "UI.View", ui);
        var service = AddNode(model, "Core.Service", core);
        var dto = AddNode(model, "Shared.Dto", shared);
        AddLink(model, view, service);
        AddLink(model, service, dto);

        var result = PathFinderService.Compute(model, ui, shared);

        var path = Assert.Single(result.Paths);
        Assert.Equal(["View", "Service", "Dto"], Names(path));
        // The lines between the assemblies carry those links, so the chain shows at overview zoom too.
        Assert.Contains(LineId.From(ui.Name, core.Name), path.LineIds);
        Assert.Contains(LineId.From(core.Name, shared.Name), path.LineIds);
    }

    [Fact]
    public void Compute_ShouldReportNotFound_WhenNoChainExists()
    {
        using var model = modelMgr.UseModel();
        var a = AddNode(model, "A", model.Root);
        var b = AddNode(model, "B", model.Root);
        AddLink(model, b, a); // Only the other direction

        var result = PathFinderService.Compute(model, a, b);

        Assert.Equal(PathStatus.NotFound, result.Status);
        Assert.Empty(result.Paths);
    }

    [Fact]
    public void Compute_ShouldReportNested_WhenOneEndpointContainsTheOther()
    {
        using var model = modelMgr.UseModel();
        var a = AddNode(model, "A", model.Root);
        var inner = AddNode(model, "A.X", a);

        Assert.Equal(PathStatus.Nested, PathFinderService.Compute(model, a, inner).Status);
        Assert.Equal(PathStatus.Nested, PathFinderService.Compute(model, inner, a).Status);
        Assert.Equal(PathStatus.Nested, PathFinderService.Compute(model, a, a).Status);
    }

    [Fact]
    public void Compute_ShouldCapTheNumberOfChains()
    {
        using var model = modelMgr.UseModel();
        var a = AddNode(model, "A", model.Root);
        var z = AddNode(model, "Z", model.Root);
        for (var i = 0; i < 15; i++)
        {
            var middle = AddNode(model, $"M{i:00}", model.Root);
            AddLink(model, a, middle);
            AddLink(model, middle, z);
        }

        var result = PathFinderService.Compute(model, a, z, maxPaths: 4);

        Assert.Equal(4, result.Paths.Count);
        Assert.All(result.Paths, p => Assert.Equal(2, p.HopCount));
    }

    [Fact]
    public void GetPathLineIds_ShouldBeNullUntilOpenWithBothEndpoints_AndFollowTheSelectedChain()
    {
        Node a,
            b,
            c,
            d;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            b = AddNode(model, "B", model.Root);
            c = AddNode(model, "C", model.Root);
            d = AddNode(model, "D", model.Root);
            AddLink(model, a, b);
            AddLink(model, b, d);
            AddLink(model, a, c);
            AddLink(model, c, d);
        }

        var service = new PathFinderService(
            modelMgr,
            Moq.Mock.Of<IModelService>(),
            Moq.Mock.Of<IApplicationEvents>(),
            new ViewOptions()
        );
        Assert.Null(service.GetPathLineIds());

        service.OpenFrom(a.Id);
        Assert.True(service.IsOpen);
        Assert.Equal(PathStatus.Incomplete, service.GetResult().Status);
        Assert.Null(service.GetPathLineIds());

        service.SetTo(d.Id);
        Assert.Contains(LineId.From(a.Name, b.Name), service.GetPathLineIds()!);
        Assert.DoesNotContain(LineId.From(a.Name, c.Name), service.GetPathLineIds()!);

        service.SelectPath(1);
        Assert.Contains(LineId.From(a.Name, c.Name), service.GetPathLineIds()!);

        service.Swap();
        Assert.Equal(PathStatus.NotFound, service.GetResult().Status);
        Assert.Null(service.GetPathLineIds());

        service.Close();
        Assert.False(service.IsOpen);
        Assert.Null(service.GetPathLineIds());
    }
}
