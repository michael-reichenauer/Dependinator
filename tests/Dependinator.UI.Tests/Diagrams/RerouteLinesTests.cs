using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Editing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Layout;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// "Re-route line" (line toolbar) and "Reset Lines" (node menu) route lines again around the
// nodes where they stand, hand-set bends included, as one undo step (LineRoutes).
public class RerouteLinesTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly CommandService commandService;
    readonly LineEditService service;
    readonly Node parent;
    readonly Node a;
    readonly Node b;
    readonly Node c;
    readonly Node d;

    public RerouteLinesTests()
    {
        commandService = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);
        service = new LineEditService(modelMgr, commandService);
        using var model = modelMgr.UseModel();
        parent = AddNode(model, model.Root, "P", new Rect(0, 0, 1000, 1000));
        a = AddNode(model, parent, "P.A", new Rect(0, 0, 100, 100));
        b = AddNode(model, parent, "P.B", new Rect(200, 0, 100, 100)); // between A and C
        c = AddNode(model, parent, "P.C", new Rect(400, 0, 100, 100));
        d = AddNode(model, parent, "P.D", new Rect(400, 300, 100, 100));
        new LineService().AddLinesFromSourceToTarget(model, new Link(a, c));
        new LineService().AddLinesFromSourceToTarget(model, new Link(a, d));
    }

    Line LineTo(Node target) =>
        modelMgr.WithModel(model => model.Lines.Values.Single(line => line.Source == a && line.Target == target));

    bool IsClear(Line line, Node target) =>
        !FreeLineRouter.IsBlocked(
            [
                new Pos(a.Boundary.X + a.Boundary.Width, 50),
                .. line.SegmentPoints,
                new Pos(target.Boundary.X, target.Boundary.Y + 50),
            ],
            [b.Boundary]
        );

    [Fact]
    public async Task RerouteLine_ShouldRouteAroundNodes_AndHandTheLineBackToTheLayout()
    {
        var line = LineTo(c);
        line.SetSegmentPoints([new Pos(250, 50)]); // a hand-set bend inside B
        line.IsSegmentsUserSet = true;

        Assert.True(service.CanRerouteLine(line.Id));
        service.RerouteLine(line.Id);

        Assert.NotEmpty(line.SegmentPoints);
        Assert.False(line.IsSegmentsUserSet);
        Assert.True(IsClear(line, c));

        await commandService.Undo();

        Assert.Equal([new Pos(250, 50)], line.SegmentPoints);
        Assert.True(line.IsSegmentsUserSet);
        Assert.False(commandService.CanUndo);
    }

    [Fact]
    public void RerouteLine_ShouldDoNothing_WhenTheLayoutLineIsAlreadyRouted()
    {
        var line = LineTo(d);
        service.RerouteLine(line.Id);
        Assert.False(commandService.CanUndo); // the straight line to D was already clear

        Assert.Empty(line.SegmentPoints);
    }

    [Fact]
    public async Task ResetLines_ShouldRouteEveryLineInside_InOneUndoStep()
    {
        var toC = LineTo(c);
        var toD = LineTo(d);
        toD.SetSegmentPoints([new Pos(150, 600)]); // a hand-set detour it doesn't need
        toD.IsSegmentsUserSet = true;

        service.ResetLines(parent.Id);

        Assert.True(IsClear(toC, c));
        Assert.NotEmpty(toC.SegmentPoints);
        Assert.Empty(toD.SegmentPoints);
        Assert.False(toD.IsSegmentsUserSet);
        Assert.Equal(new Rect(200, 0, 100, 100), b.Boundary); // the nodes stay

        await commandService.Undo();

        Assert.Empty(toC.SegmentPoints);
        Assert.Equal([new Pos(150, 600)], toD.SegmentPoints);
        Assert.True(toD.IsSegmentsUserSet);
        Assert.False(commandService.CanUndo);
    }

    [Fact]
    public void ResetLines_ShouldHandBackAHandSetLine_ThatIsAlreadyWhereTheLayoutPutsIt()
    {
        var toD = LineTo(d);
        toD.IsSegmentsUserSet = true;

        service.ResetLines(parent.Id);

        Assert.False(toD.IsSegmentsUserSet);
    }

    [Fact]
    public void CanRerouteLine_ShouldBeFalse_ForALineBetweenMembers()
    {
        Line memberLine;
        using (var model = modelMgr.UseModel())
        {
            var m1 = AddNode(model, a, "P.A.M1", new Rect(0, 0, 50, 20));
            var m2 = AddNode(model, a, "P.A.M2", new Rect(0, 30, 50, 20));
            m1.Type = NodeType.MethodMember;
            m2.Type = NodeType.MethodMember;
            new LineService().AddLinesFromSourceToTarget(model, new Link(m1, m2));
            memberLine = model.Lines.Values.Single(line => line.Source == m1 && line.Target == m2);
        }

        Assert.False(service.CanRerouteLine(memberLine.Id));
    }

    static Node AddNode(IModel model, Node parent, string name, Rect boundary)
    {
        var node = new Node(name, parent) { Boundary = boundary, Type = NodeType.ClassType };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }
}
