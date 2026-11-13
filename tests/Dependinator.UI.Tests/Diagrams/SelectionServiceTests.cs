using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Group selection: Shift/Ctrl+click adds a node to the selection (and removes it again), the
// primary node stays the toolbar's anchor, hiding applies to the whole group, and clearing the
// selection clears every node's selected flag.
public class SelectionServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly Mock<IScreenService> screenService = new();
    readonly Mock<IApplicationEvents> applicationEvents = new();

    public SelectionServiceTests()
    {
        screenService.Setup(s => s.SvgRect).Returns(new Rect(0, 0, 800, 600));
        screenService
            .Setup(s => s.GetBoundingRectangle(It.IsAny<string>()))
            .ReturnsAsync((Result<ElementBoundingRectangle>)new Error("Not on screen"));
        modelMgr.WithModel(m => m.Zoom = 1);
        commandService = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);
    }

    static Node AddNode(IModel model, string name, Node parent)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100), Type = NodeType.ClassType };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    readonly CommandService commandService;

    SelectionService CreateService() => new(modelMgr, applicationEvents.Object, screenService.Object, commandService);

    static PointerEvent ShiftClick => new() { ShiftKey = true };

    [Fact]
    public async Task ToggleInSelection_ShouldAddAndRemoveNodes_AroundThePrimaryOne()
    {
        Node a,
            b,
            c;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            b = AddNode(model, "B", model.Root);
            c = AddNode(model, "C", model.Root);
        }
        var service = CreateService();

        await service.Select(a.Id);
        await service.ToggleInSelectionAsync(PointerId.FromNode(b.Id), ShiftClick);
        await service.ToggleInSelectionAsync(PointerId.FromNode(c.Id), ShiftClick);

        Assert.Equal(PointerId.FromNode(a.Id), service.SelectedId);
        Assert.Equal([a.Id, b.Id, c.Id], service.SelectedNodeIds);
        Assert.Equal(3, service.SelectedNodeCount);
        Assert.True(b.IsSelected && c.IsSelected);

        await service.ToggleInSelectionAsync(PointerId.FromNode(b.Id), ShiftClick);
        Assert.Equal([a.Id, c.Id], service.SelectedNodeIds);
        Assert.False(b.IsSelected);

        // Shift+clicking the primary node keeps the selection as it is.
        await service.ToggleInSelectionAsync(PointerId.FromNode(a.Id), ShiftClick);
        Assert.Equal([a.Id, c.Id], service.SelectedNodeIds);

        service.Unselect();
        Assert.Empty(service.SelectedNodeIds);
        Assert.False(a.IsSelected || c.IsSelected);
    }

    [Fact]
    public async Task ToggleInSelection_ShouldSelectNormally_WhenNothingIsSelected()
    {
        Node a;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
        }
        var service = CreateService();

        await service.ToggleInSelectionAsync(PointerId.FromNode(a.Id), ShiftClick);

        Assert.Equal([a.Id], service.SelectedNodeIds);
        Assert.True(a.IsSelected);
    }

    [Fact]
    public async Task SetSelectedNodesVisibility_ShouldHideTheWholeGroup_AndShowItAgain()
    {
        Node a,
            b;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            b = AddNode(model, "B", model.Root);
        }
        var service = CreateService();
        await service.Select(a.Id);
        await service.ToggleInSelectionAsync(PointerId.FromNode(b.Id), ShiftClick);

        service.SetSelectedNodesVisibility(NodeVisibility.Hidden);
        Assert.True(!a.IsVisible && !b.IsVisible);
        Assert.Equal(NodeVisibility.Hidden, service.SelectedNodeVisibility());

        service.SetSelectedNodesVisibility(NodeVisibility.Visible);
        Assert.True(a.IsVisible && b.IsVisible);

        // Each change is one undo step.
        await commandService.Undo();
        Assert.True(!a.IsVisible && !b.IsVisible);
        await commandService.Undo();
        Assert.True(a.IsVisible && b.IsVisible);
        Assert.False(commandService.CanUndo);
    }

    [Fact]
    public async Task AddToSelection_ShouldMakeTheFirstPrimary_AndExtendAnExistingSelection()
    {
        Node a,
            b,
            c;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            b = AddNode(model, "B", model.Root);
            c = AddNode(model, "C", model.Root);
        }
        var service = CreateService();

        await service.AddToSelectionAsync([a.Id, b.Id]);
        Assert.Equal(PointerId.FromNode(a.Id), service.SelectedId);
        Assert.Equal([a.Id, b.Id], service.SelectedNodeIds);

        await service.AddToSelectionAsync([b.Id, c.Id]); // B already in: only C is new
        Assert.Equal([a.Id, b.Id, c.Id], service.SelectedNodeIds);
        Assert.True(a.IsSelected && b.IsSelected && c.IsSelected);
    }

    [Fact]
    public async Task Select_ShouldReplaceAGroupSelection()
    {
        Node a,
            b;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            b = AddNode(model, "B", model.Root);
        }
        var service = CreateService();
        await service.Select(a.Id);
        await service.ToggleInSelectionAsync(PointerId.FromNode(b.Id), ShiftClick);

        await service.Select(b.Id);

        Assert.Equal([b.Id], service.SelectedNodeIds);
        Assert.False(a.IsSelected);
        Assert.True(b.IsSelected);
    }

    // A selected stub's lines are drawn end to end only while it is selected, so one of them is
    // on screen (can be measured) until the stub is deselected and the diagram repainted.
    [Fact]
    public async Task Select_ShouldSelectALineOfTheSelectedStub_ThoughDeselectingTheStubTakesItOffScreen()
    {
        Line stub;
        Line line;
        using (var model = modelMgr.UseModel())
        {
            var a = AddNode(model, "A", model.Root);
            var b = AddNode(model, "B", model.Root);
            var child = AddNode(model, "Child", b);
            stub = new Line(b, child); // From the container's edge into its child
            line = new Line(a, child); // One of the stub's lines, from the node that uses the child
            model.TryAddLine(stub);
            model.TryAddLine(line);
        }
        var stubPointer = PointerId.FromLine(stub.Id);
        var linePointer = PointerId.FromLine(line.Id);
        var onScreen = new ElementBoundingRectangle
        {
            X = 10,
            Y = 10,
            Width = 100,
            Height = 50,
            Right = 110,
            Bottom = 60,
        };
        screenService.Setup(s => s.GetBoundingRectangle(stubPointer.ElementId)).ReturnsAsync(onScreen);
        screenService
            .Setup(s => s.GetBoundingRectangle(linePointer.ElementId))
            .ReturnsAsync(() =>
                stub.IsSelected || line.IsSelected
                    ? new ElementBoundingRectangle
                    {
                        X = 10,
                        Y = 10,
                        Width = 100,
                        Height = 50,
                        Right = 110,
                        Bottom = 60,
                    }
                    : (Result<ElementBoundingRectangle>)new Error("Not on screen")
            );
        var service = CreateService();
        await service.Select(stubPointer, new PointerEvent { ClientX = 60, ClientY = 35 });
        Assert.True(stub.IsSelected);
        applicationEvents.Invocations.Clear();

        await service.Select(linePointer, new PointerEvent { ClientX = 60, ClientY = 35 });

        Assert.Equal(linePointer, service.SelectedId);
        Assert.True(line.IsSelected);
        Assert.False(stub.IsSelected);
        Assert.Empty(service.SelectedNodeIds);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.AtLeastOnce);
        applicationEvents.Verify(e => e.TriggerUIStateChanged(), Times.AtLeastOnce);
    }

    // Clicking one of a selected stub's lines keeps the stub's other lines (the stub is explored),
    // while the user goes from line to line; Escape's first step selects the stub again, and any
    // other selection ends it.
    [Fact]
    public async Task Select_ShouldKeepExploringTheStub_WhileGoingFromLineToLine()
    {
        Node a;
        Line stub;
        Line first;
        Line second;
        Line other;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            var c = AddNode(model, "C", model.Root);
            var b = AddNode(model, "B", model.Root);
            var child = AddNode(model, "Child", b);
            stub = new Line(b, child);
            first = new Line(a, child) { RenderAncestor = model.Root };
            second = new Line(c, child) { RenderAncestor = model.Root };
            other = new Line(a, c) { RenderAncestor = model.Root }; // Not one of the stub's lines
            var fromA = new Link(a, child);
            var fromC = new Link(c, child);
            stub.Add(fromA);
            stub.Add(fromC);
            first.Add(fromA);
            second.Add(fromC);
            other.Add(new Link(a, c));
            foreach (var line in new[] { stub, first, second, other })
                model.TryAddLine(line);
        }
        screenService
            .Setup(s => s.GetBoundingRectangle(It.IsAny<string>()))
            .ReturnsAsync(
                new ElementBoundingRectangle
                {
                    X = 10,
                    Y = 10,
                    Width = 100,
                    Height = 50,
                    Right = 110,
                    Bottom = 60,
                }
            );
        var service = CreateService();
        var click = new PointerEvent { ClientX = 60, ClientY = 35 };

        await service.Select(PointerId.FromLine(stub.Id), new PointerEvent { ClientX = 20, ClientY = 30 });
        Assert.False(service.TrySelectExploredStub()); // Nothing to step back to

        await service.Select(PointerId.FromLine(first.Id), click);
        Assert.True(first.IsSelected);
        Assert.True(stub.IsExplored);

        await service.Select(PointerId.FromLine(second.Id), click);
        Assert.True(second.IsSelected);
        Assert.False(first.IsSelected);
        Assert.True(stub.IsExplored);

        // Escape steps back to the stub, which shows its lines as the selected stub.
        Assert.True(service.TrySelectExploredStub());
        Assert.Equal(PointerId.FromLine(stub.Id), service.SelectedId);
        Assert.True(stub.IsSelected);
        Assert.False(second.IsSelected);
        Assert.False(stub.IsExplored);
        Assert.Equal(new Pos(20, 30), service.SelectedLineClickPosition); // Where the stub was clicked
        Assert.False(service.TrySelectExploredStub());

        // A line that was not drawn for the stub ends it, and so does a node.
        await service.Select(PointerId.FromLine(first.Id), click);
        Assert.True(stub.IsExplored);
        await service.Select(PointerId.FromLine(other.Id), click);
        Assert.False(stub.IsExplored);
        Assert.True(other.IsSelected);

        await service.Select(PointerId.FromLine(stub.Id), click);
        await service.Select(PointerId.FromLine(first.Id), click);
        Assert.True(stub.IsExplored);
        await service.Select(a.Id);
        Assert.False(stub.IsExplored);
        Assert.False(first.IsSelected);
    }

    [Fact]
    public async Task TrySelectExploredStub_ShouldNotClearTheSelection_WhenTheStubIsGone()
    {
        Line stub;
        Line line;
        using (var model = modelMgr.UseModel())
        {
            var a = AddNode(model, "A", model.Root);
            var b = AddNode(model, "B", model.Root);
            var child = AddNode(model, "Child", b);
            stub = new Line(b, child);
            line = new Line(a, child) { RenderAncestor = model.Root };
            var link = new Link(a, child);
            stub.Add(link);
            line.Add(link);
            model.TryAddLine(stub);
            model.TryAddLine(line);
        }
        screenService
            .Setup(s => s.GetBoundingRectangle(It.IsAny<string>()))
            .ReturnsAsync(
                new ElementBoundingRectangle
                {
                    X = 10,
                    Y = 10,
                    Width = 100,
                    Height = 50,
                    Right = 110,
                    Bottom = 60,
                }
            );
        var service = CreateService();
        var click = new PointerEvent { ClientX = 60, ClientY = 35 };
        await service.Select(PointerId.FromLine(stub.Id), click);
        await service.Select(PointerId.FromLine(line.Id), click);
        Assert.True(stub.IsExplored);

        // A refresh took the stub's line away: Escape has no step back and clears as usual.
        modelMgr.WithModel(model => model.RemoveLine(stub));

        Assert.False(service.TrySelectExploredStub());
        Assert.Equal(PointerId.FromLine(line.Id), service.SelectedId);
        Assert.True(line.IsSelected);
    }

    [Fact]
    public async Task Select_ShouldClearTheSelection_WhenTheClickedLineIsNotOnScreen()
    {
        Node a;
        Line line;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            var b = AddNode(model, "B", model.Root);
            line = new Line(a, b);
            model.TryAddLine(line);
        }
        var service = CreateService();
        await service.Select(a.Id);

        await service.Select(PointerId.FromLine(line.Id), new PointerEvent());

        Assert.False(service.IsSelected);
        Assert.False(a.IsSelected);
        Assert.False(line.IsSelected);
    }

    [Fact]
    public async Task IsSelectedNodeMovable_ShouldBeTrueForAZone_HoweverMuchOfTheScreenItCovers()
    {
        // A zone is drawn around a group and often fills the view; a plain node that large reads
        // as "the screen" and dragging it pans instead.
        Node zone,
            box;
        using (var model = modelMgr.UseModel())
        {
            zone = AddNode(model, "Edge", model.Root);
            zone.IsZone = true;
            zone.IsManual = true;
            zone.Boundary = new Rect(0, 0, 100000, 100000);
            box = AddNode(model, "Box", model.Root);
            box.Boundary = new Rect(0, 0, 100000, 100000);
        }
        var service = CreateService();

        await service.Select(zone.Id);
        Assert.True(service.IsSelected);
        Assert.True(service.IsSelectedNodeMovable(1));

        service.Unselect();
        await service.Select(box.Id);
        Assert.False(service.IsSelected); // too large to be selected at all
    }
}
