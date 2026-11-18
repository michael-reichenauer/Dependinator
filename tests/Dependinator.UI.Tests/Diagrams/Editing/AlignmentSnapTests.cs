using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Editing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams.Editing;

// Smart guides: a dragged node snaps to a nearby sibling's edge or centre and a guide shows it.
public class AlignmentSnapTests
{
    const double Snap = 7;
    const double Reach = 600;

    static AlignmentSnap.Result Find(Rect moving, params Rect[] others) =>
        AlignmentSnap.Find(moving, others, Snap, Reach, overshoot: 0);

    [Fact]
    public void Find_ShouldAlignLeftEdges_AndDrawOneGuideThroughBothCentres()
    {
        var result = Find(new Rect(104, 300, 100, 100), new Rect(100, 100, 100, 100));

        Assert.Equal(-4, result.Dx, 6);
        Assert.Equal(0, result.Dy);
        Assert.Equal(new AlignmentSnap.Guide(150, 100, 400), result.Vertical);
        Assert.Null(result.Horizontal);
    }

    [Fact]
    public void Find_ShouldAlignCentres_OfNodesOfDifferentSizes()
    {
        // A 60 wide node under a 140 wide one: its centre 133 comes near the other's 130.
        var result = Find(new Rect(103, 300, 60, 60), new Rect(60, 100, 140, 140));

        Assert.Equal(-3, result.Dx, 6);
        Assert.Equal(130, result.Vertical?.At ?? 0, 6);
    }

    [Fact]
    public void Find_ShouldAlignAnEdgeWithTheOtherBoxsOppositeEdge()
    {
        // Side by side: the moving box's top comes near the other's bottom (stacked), and its
        // left edge near the other's right edge.
        var result = Find(new Rect(205, 205, 100, 100), new Rect(100, 100, 100, 100));

        Assert.Equal(-5, result.Dx, 6);
        Assert.Equal(-5, result.Dy, 6);
        Assert.Equal(200, result.Vertical?.At ?? 0, 6);
        Assert.Equal(200, result.Horizontal?.At ?? 0, 6);
    }

    [Fact]
    public void Find_ShouldDoNothing_WhenNothingIsCloseEnoughOrNearEnough()
    {
        Assert.Equal(AlignmentSnap.Result.None, Find(new Rect(110, 300, 100, 100), new Rect(100, 100, 100, 100)));
        Assert.Equal(AlignmentSnap.Result.None, Find(new Rect(102, 1000, 100, 100), new Rect(100, 100, 100, 100)));
        Assert.Equal(AlignmentSnap.Result.None, Find(new Rect(102, 300, 100, 100)));
    }

    [Fact]
    public void Find_ShouldTakeTheClosestMatch_AndOnATieTheNearerNode()
    {
        // Left edges 2 off one node and 5 off another: the closer one wins.
        var closest = Find(new Rect(102, 300, 100, 100), new Rect(100, 100, 100, 100), new Rect(97, 500, 100, 100));
        Assert.Equal(-2, closest.Dx, 6);

        // Both 3 off: the node right above wins over the one further down.
        var nearer = Find(new Rect(103, 300, 100, 100), new Rect(100, 600, 100, 100), new Rect(100, 180, 100, 100));
        Assert.Equal(-3, nearer.Dx, 6);
        Assert.Equal(new AlignmentSnap.Guide(150, 180, 700), nearer.Vertical); // Through both
    }

    [Fact]
    public void Find_ShouldRunTheGuideThroughEveryNodeOnTheLine()
    {
        var result = Find(
            new Rect(102, 300, 100, 100),
            new Rect(100, 100, 100, 100),
            new Rect(100, 500, 100, 100),
            new Rect(400, 300, 100, 100)
        );

        Assert.Equal(new AlignmentSnap.Guide(150, 100, 600), result.Vertical);
    }

    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly CommandService commandService;
    readonly AlignmentGuideService guides = new(Mock.Of<IApplicationEvents>());
    readonly NodeEditService service;

    public AlignmentSnapTests()
    {
        commandService = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);
        service = new NodeEditService(modelMgr, commandService, guides);
    }

    Node AddNode(string name, Rect boundary)
    {
        using var model = modelMgr.UseModel();
        var node = new Node(name, model.Root) { Boundary = boundary, Type = NodeType.ClassType };
        model.Root.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    // Screen pixels for a distance in the root's children units (zoom 1).
    double Pixels(double units) => units * modelMgr.WithModel(m => m.Root.ContainerZoom);

    [Fact]
    public void MoveSelectedNode_ShouldSnapToASibling_AndKeepTheAlignmentOverTheGrid()
    {
        // The sibling is off the grid (x 110), so the grid snap would break the alignment.
        AddNode("A", new Rect(110, 100, 60, 60));
        var b = AddNode("B", new Rect(200, 300, 60, 60));
        var pointer = PointerId.FromNode(b.Id);

        service.BeginMove();
        service.MoveSelectedNode(new PointerEvent { MovementX = Pixels(-86) }, zoom: 1, pointer);

        Assert.Equal(110, b.Boundary.X, 6);
        Assert.Single(guides.Lines);

        service.SnapSelectedNodeToGrid(pointer);
        service.EndMove();

        Assert.Equal(110, b.Boundary.X, 6); // Aligned, not on the grid
        Assert.Equal(300, b.Boundary.Y, 6);
        Assert.Empty(guides.Lines);
    }

    [Fact]
    public void MoveSelectedNode_ShouldLetGo_WhenThePointerMovesOn()
    {
        AddNode("A", new Rect(110, 100, 60, 60));
        var b = AddNode("B", new Rect(200, 300, 60, 60));
        var pointer = PointerId.FromNode(b.Id);

        service.BeginMove();
        service.MoveSelectedNode(new PointerEvent { MovementX = Pixels(-86) }, zoom: 1, pointer); // Raw 114: snaps
        service.MoveSelectedNode(new PointerEvent { MovementX = Pixels(-30) }, zoom: 1, pointer); // Raw 84: free

        Assert.Equal(84, b.Boundary.X, 6);
        Assert.Empty(guides.Lines);

        service.SnapSelectedNodeToGrid(pointer);
        service.EndMove();
        Assert.Equal(80, b.Boundary.X, 6); // The grid again
    }

    [Fact]
    public void MoveSelectedNodes_ShouldShiftTheWholeGroupWithItsLead()
    {
        AddNode("A", new Rect(110, 100, 60, 60));
        var b = AddNode("B", new Rect(200, 300, 60, 60));
        var c = AddNode("C", new Rect(300, 300, 60, 60));

        service.BeginMove();
        service.MoveSelectedNodes(new PointerEvent { MovementX = Pixels(-86) }, zoom: 1, [b.Id, c.Id], b.Id);

        Assert.Equal(110, b.Boundary.X, 6);
        Assert.Equal(210, c.Boundary.X, 6);

        service.SnapSelectedNodesToGrid([b.Id, c.Id]);
        service.EndMove();
        Assert.Equal(110, b.Boundary.X, 6);
        Assert.Equal(210, c.Boundary.X, 6);
    }

    [Fact]
    public void MoveSelectedNode_ShouldGoOnFromWhereANodeIs_WhenSomethingElseMovedItDuringTheDrag()
    {
        AddNode("A", new Rect(110, 100, 60, 60));
        var b = AddNode("B", new Rect(200, 300, 60, 60));
        var pointer = PointerId.FromNode(b.Id);

        service.BeginMove();
        service.MoveSelectedNode(new PointerEvent { MovementX = Pixels(-86) }, zoom: 1, pointer); // Snaps to 110
        b.Boundary = b.Boundary with { X = 400 }; // Say a refresh moved it
        service.MoveSelectedNode(new PointerEvent { MovementX = Pixels(20) }, zoom: 1, pointer);

        Assert.Equal(420, b.Boundary.X, 6);
    }

    [Fact]
    public void SnapSelectedNodesToGrid_ShouldSnapAGroupNodeInAnotherContainer_AlsoOnAnAlignedAxis()
    {
        AddNode("A", new Rect(110, 100, 60, 60));
        var b = AddNode("B", new Rect(200, 300, 60, 60));
        var other = AddNode("P", new Rect(1000, 1000, 200, 200));
        Node c;
        using (var model = modelMgr.UseModel())
        {
            c = new Node("P.C", other) { Boundary = new Rect(40, 40, 60, 60), Type = NodeType.ClassType };
            other.AddChild(c);
            model.TryAddNode(c);
        }

        service.BeginMove();
        service.MoveSelectedNodes(new PointerEvent { MovementX = Pixels(-86) }, zoom: 1, [b.Id, c.Id], b.Id);
        service.SnapSelectedNodesToGrid([b.Id, c.Id]);

        Assert.Equal(110, b.Boundary.X, 6); // Aligned
        Assert.Equal(NodeGrid.Snap(c.Boundary.X), c.Boundary.X, 6); // Its own container's grid
    }
}
