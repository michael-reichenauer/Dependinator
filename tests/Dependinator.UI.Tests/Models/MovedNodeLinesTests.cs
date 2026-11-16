using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Editing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Layout;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

// When a move ends, the layout's bends follow the moved nodes (MovedNodeLines), in the move's
// undo step; the user's own bends stay, unless both ends of their line moved together.
public class MovedNodeLinesTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly CommandService commandService;
    readonly NodeEditService service;
    readonly Node parent;
    readonly Node a;
    readonly Node b;
    readonly Node c;

    public MovedNodeLinesTests()
    {
        commandService = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);
        service = new NodeEditService(
            modelMgr,
            commandService,
            new AlignmentGuideService(Mock.Of<IApplicationEvents>())
        );
        using var model = modelMgr.UseModel();
        parent = AddNode(model, model.Root, "P", new Rect(0, 0, 1000, 1000));
        a = AddNode(model, parent, "P.A", new Rect(0, 0, 100, 100));
        b = AddNode(model, parent, "P.B", new Rect(200, 300, 100, 100));
        c = AddNode(model, parent, "P.C", new Rect(400, 0, 100, 100));
        new LineService().AddLinesFromSourceToTarget(model, new Link(a, c));
    }

    Line LineAC => modelMgr.WithModel(model => model.Lines.Values.Single(line => line.Source == a && line.Target == c));

    // Drags the node by the distance (in its container's units) and drops it.
    void Drag(Node node, double dx, double dy)
    {
        var zoom = node.GetZoom();
        service.MoveSelectedNode(
            new PointerEvent { MovementX = dx / zoom, MovementY = dy / zoom },
            zoom: 1,
            PointerId.FromNode(node.Id)
        );
        service.SnapSelectedNodeToGrid(PointerId.FromNode(node.Id));
        service.EndMove();
    }

    [Fact]
    public async Task EndMove_ShouldRouteALineAroundANodeMovedIntoItsWay_InTheMoveUndoStep()
    {
        Drag(b, 0, -300); // B now stands between A and C

        Assert.NotEmpty(LineAC.SegmentPoints);
        Assert.False(LineAC.IsSegmentsUserSet);
        Assert.False(
            FreeLineRouter.IsBlocked([new Pos(100, 50), .. LineAC.SegmentPoints, new Pos(400, 50)], [b.Boundary])
        );

        await commandService.Undo();

        Assert.Equal(new Rect(200, 300, 100, 100), b.Boundary);
        Assert.Empty(LineAC.SegmentPoints);
        Assert.False(commandService.CanUndo);
    }

    [Fact]
    public void EndMove_ShouldStraightenALine_WhenTheNodeItWentAroundMovesAway()
    {
        b.Boundary = new Rect(200, 0, 100, 100);
        LineAC.SetSegmentPoints([new Pos(250, -35)]);

        Drag(b, 0, 300);

        Assert.Empty(LineAC.SegmentPoints);
    }

    [Fact]
    public void EndMove_ShouldKeepTheUsersBends()
    {
        var bends = new[] { new Pos(250, 500) };
        LineAC.SetSegmentPoints(bends);
        LineAC.IsSegmentsUserSet = true;

        Drag(c, 0, 200);

        Assert.Equal(bends, LineAC.SegmentPoints);
        Assert.True(LineAC.IsSegmentsUserSet);
    }

    [Fact]
    public void EndMove_ShouldRouteTheLineOfTheMovedNode()
    {
        LineAC.SetSegmentPoints([new Pos(250, 500)]); // A detour the layout made for an old spot

        Drag(c, 0, 200);

        Assert.Empty(LineAC.SegmentPoints); // Nothing in the way of the new straight line
    }

    [Fact]
    public async Task EndMove_ShouldMoveTheBendsOfALineBetweenNodesMovedTogether()
    {
        LineAC.SetSegmentPoints([new Pos(250, 500)]);
        LineAC.IsSegmentsUserSet = true;
        var zoom = a.GetZoom();

        service.MoveSelectedNodes(new PointerEvent { MovementX = 40 / zoom, MovementY = 20 / zoom }, 1, [a.Id, c.Id]);
        service.SnapSelectedNodesToGrid([a.Id, c.Id]);
        service.EndMove();

        Assert.Equal([new Pos(290, 520)], LineAC.SegmentPoints);
        Assert.True(LineAC.IsSegmentsUserSet);

        await commandService.Undo();
        Assert.Equal([new Pos(250, 500)], LineAC.SegmentPoints);
        Assert.Equal(new Rect(0, 0, 100, 100), a.Boundary);
        Assert.False(commandService.CanUndo);
    }

    [Fact]
    public void EndMove_ShouldLeaveLinesAlone_WhenNothingChanges()
    {
        Drag(b, 0, 100); // Far from the line

        Assert.Empty(LineAC.SegmentPoints);
        Assert.True(commandService.CanUndo);
    }

    [Fact]
    public void EndMove_ShouldRouteEveryLineAroundTheMovedNode()
    {
        Node d,
            e;
        using (var model = modelMgr.UseModel())
        {
            d = AddNode(model, parent, "P.D", new Rect(0, 200, 100, 100));
            e = AddNode(model, parent, "P.E", new Rect(400, 200, 100, 100));
            new LineService().AddLinesFromSourceToTarget(model, new Link(d, e));
        }
        b.Boundary = new Rect(200, 400, 100, 300);
        var lineDE = modelMgr.WithModel(model =>
            model.Lines.Values.Single(line => line.Source == d && line.Target == e)
        );

        Drag(b, 0, -400); // B now stands in the way of both lines

        Assert.NotEmpty(LineAC.SegmentPoints);
        Assert.NotEmpty(lineDE.SegmentPoints);
        Assert.False(
            FreeLineRouter.IsBlocked([new Pos(100, 50), .. LineAC.SegmentPoints, new Pos(400, 50)], [b.Boundary])
        );
        Assert.False(
            FreeLineRouter.IsBlocked([new Pos(100, 250), .. lineDE.SegmentPoints, new Pos(400, 250)], [b.Boundary])
        );
    }

    [Fact]
    public async Task EndMove_ShouldUndoTheWholeMoveInOneStep_AlsoAfterAPause()
    {
        var zoom = b.GetZoom();
        var pointerId = PointerId.FromNode(b.Id);
        service.BeginMove();
        service.MoveSelectedNode(new PointerEvent { MovementX = 0, MovementY = -150 / zoom }, 1, pointerId);
        await Task.Delay(600); // Longer than the merge time of quick edits
        service.MoveSelectedNode(new PointerEvent { MovementX = 0, MovementY = -150 / zoom }, 1, pointerId);
        service.SnapSelectedNodeToGrid(pointerId);
        service.EndMove();
        Assert.NotEmpty(LineAC.SegmentPoints);

        await commandService.Undo();

        Assert.Equal(new Rect(200, 300, 100, 100), b.Boundary);
        Assert.Empty(LineAC.SegmentPoints);
        Assert.False(commandService.CanUndo);
    }

    [Fact]
    public void BeginMove_ShouldForgetAMoveWhoseEndWasMissed()
    {
        var zoom = c.GetZoom();
        service.MoveSelectedNode(
            new PointerEvent { MovementX = 0, MovementY = 400 / zoom },
            1,
            PointerId.FromNode(c.Id)
        );
        LineAC.SetSegmentPoints([new Pos(250, 500)]); // Bends that C's missed move end would reroute

        service.BeginMove();
        service.EndMove();

        Assert.Equal([new Pos(250, 500)], LineAC.SegmentPoints);
    }

    static Node AddNode(IModel model, Node parent, string name, Rect boundary)
    {
        var node = new Node(name, parent) { Boundary = boundary, Type = NodeType.ClassType };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }
}
