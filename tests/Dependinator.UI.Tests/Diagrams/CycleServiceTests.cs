using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Cycle detection: per container, children that depend on each other in a circle form a cycle,
// links deep inside the children count for their top-level siblings, and one-way chains do not.
public class CycleServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

    static Node AddNode(IModel model, string name, Node parent)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100) };
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

    [Fact]
    public void GetCycles_ShouldFindMutuallyDependentSiblings_AndTheirLines()
    {
        Node a,
            b,
            d,
            container;
        using (var model = modelMgr.UseModel())
        {
            container = AddNode(model, "C", model.Root);
            a = AddNode(model, "C.A", container);
            b = AddNode(model, "C.B", container);
            d = AddNode(model, "C.D", container);
            AddLink(model, a, b);
            AddLink(model, b, a);
            AddLink(model, b, d); // D is a leaf of the graph: not part of the cycle
        }

        var service = new CycleService(modelMgr);
        var cycles = service.GetCycles();

        var cycle = Assert.Single(cycles);
        Assert.Equal(container.Id, cycle.ContainerId);
        Assert.Equal(["A", "B"], cycle.Members.Select(m => m.Name));

        var lineIds = service.GetCyclicLineIds();
        Assert.Contains(LineId.From(a.Name, b.Name), lineIds);
        Assert.Contains(LineId.From(b.Name, a.Name), lineIds);
        Assert.DoesNotContain(LineId.From(b.Name, d.Name), lineIds);
    }

    [Fact]
    public void GetCycles_ShouldCountLinksDeepInsideChildren_ForTheirTopLevelSiblings()
    {
        using (var model = modelMgr.UseModel())
        {
            var a = AddNode(model, "A", model.Root);
            var b = AddNode(model, "B", model.Root);
            var aInner = AddNode(model, "A.X", a);
            var bInner = AddNode(model, "B.Y", b);
            AddLink(model, aInner, bInner);
            AddLink(model, bInner, aInner);
        }

        var cycle = Assert.Single(new CycleService(modelMgr).GetCycles());
        Assert.Equal(["A", "B"], cycle.Members.Select(m => m.Name));
    }

    [Fact]
    public void GetCycles_ShouldIgnoreOneWayChains_AndParentChildLinks()
    {
        using (var model = modelMgr.UseModel())
        {
            var a = AddNode(model, "A", model.Root);
            var b = AddNode(model, "B", model.Root);
            var c = AddNode(model, "C", model.Root);
            var aInner = AddNode(model, "A.X", a);
            AddLink(model, a, b);
            AddLink(model, b, c);
            AddLink(model, aInner, a); // Inside → its own container: no sibling pair
        }

        Assert.Empty(new CycleService(modelMgr).GetCycles());
    }
}
