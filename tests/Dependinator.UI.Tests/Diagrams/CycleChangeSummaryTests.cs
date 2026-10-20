using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// What a re-parse changed about cycles: entries whose cycle is gone or grew, and open cycles that
// are new or gone (one that only grew or shrank is neither).
public class CycleChangeSummaryTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

    static Node AddNode(IModel model, string name, Node parent)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100) };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static Link AddLink(IModel model, Node source, Node target)
    {
        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
        return link;
    }

    // A pair A ↔ B in C, marked to fix, and D used by B.
    (Link BToA, Node B, Node D) AddCycleToFix()
    {
        using var model = modelMgr.UseModel();
        var container = AddNode(model, "C", model.Root);
        var a = AddNode(model, "C.A", container);
        var b = AddNode(model, "C.B", container);
        var d = AddNode(model, "C.D", container);
        AddLink(model, a, b);
        var bToA = AddLink(model, b, a);
        AddLink(model, b, d);
        model.AddAcceptedCycle(new AcceptedCycle("C", ["C.A", "C.B"], "Remove B's use of A", IsToFix: true));
        return (bToA, b, d);
    }

    [Fact]
    public void Describe_ShouldTellEachKindOfChange_AndNothingWhenNoneChanged()
    {
        Assert.Null(CycleChangeSummary.Describe(0, 0, 0, 0, 0));
        Assert.Equal("1 cycle to fix is gone", CycleChangeSummary.Describe(0, 0, 1, 0, 0));
        Assert.Equal(
            "2 cycles to fix are gone, 1 accepted cycle is gone, 1 accepted or to-fix cycle changed, 3 cycles gone, 1 new cycle",
            CycleChangeSummary.Describe(1, 3, 2, 1, 1)
        );
    }

    [Fact]
    public void Describe_ShouldTellACycleToFixIsGone_WhenARefactorBrokeIt()
    {
        var (bToA, _, _) = AddCycleToFix();
        var before = CycleChangeSummary.Capture(modelMgr);

        modelMgr.WithModel(m => m.RemoveLink(bToA));

        Assert.Equal(
            "1 cycle to fix is gone",
            CycleChangeSummary.Describe(before, CycleChangeSummary.Capture(modelMgr))
        );
    }

    [Fact]
    public void Describe_ShouldTellACycleToFixChanged_NotThatANewCycleCame_WhenANodeJoinedIt()
    {
        var (_, b, d) = AddCycleToFix();
        var before = CycleChangeSummary.Capture(modelMgr);

        modelMgr.WithModel(m => AddLink(m, d, b));

        Assert.Equal(
            "1 accepted or to-fix cycle changed",
            CycleChangeSummary.Describe(before, CycleChangeSummary.Capture(modelMgr))
        );
    }

    [Fact]
    public void Describe_ShouldTellANewCycle_AndAGoneOne()
    {
        Link xToY;
        using (var model = modelMgr.UseModel())
        {
            var container = AddNode(model, "K", model.Root);
            var x = AddNode(model, "K.X", container);
            var y = AddNode(model, "K.Y", container);
            AddLink(model, x, y);
            xToY = AddLink(model, y, x);
        }
        var before = CycleChangeSummary.Capture(modelMgr);
        Node n;
        using (var model = modelMgr.UseModel())
        {
            var other = AddNode(model, "N", model.Root);
            n = AddNode(model, "N.P", other);
            var q = AddNode(model, "N.Q", other);
            AddLink(model, n, q);
            AddLink(model, q, n);
            model.RemoveLink(xToY);
        }

        Assert.Equal(
            "1 cycle gone, 1 new cycle",
            CycleChangeSummary.Describe(before, CycleChangeSummary.Capture(modelMgr))
        );
    }
}
