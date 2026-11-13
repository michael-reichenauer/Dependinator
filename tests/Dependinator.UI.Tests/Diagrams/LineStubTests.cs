using System.Xml.Linq;
using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// A line between a container's edge and one of its children is drawn as a stub at the child: a
// short arrow with a ring at the far end. It is the same line (id, hit target, selection), only
// short; a line the user labelled or routed, or one on the shown path, is still drawn in full.
public class LineStubTests
{
    // A container with two children side by side: "a" at the left, "b" 100 units to its right.
    sealed class Scene
    {
        public Node Parent { get; }
        public Node A { get; }
        public Node B { get; }
        public Node Outside { get; }

        public Scene(NodeType childType = NodeType.ClassType)
        {
            var root = new Node("", null!) { Type = NodeType.Root, ContainerZoom = 1 };
            Parent = new Node("p", root)
            {
                Boundary = new Rect(0, 0, 1000, 1000),
                Type = NodeType.Namespace,
                ContainerZoom = 1,
            };
            root.AddChild(Parent);
            A = new Node("p.a", Parent) { Boundary = new Rect(200, 400, 100, 100), Type = childType };
            B = new Node("p.b", Parent) { Boundary = new Rect(400, 400, 100, 100), Type = childType };
            Parent.AddChild(A);
            Parent.AddChild(B);
            Outside = new Node("q", root) { Boundary = new Rect(2000, 0, 100, 100), Type = childType };
            root.AddChild(Outside);
        }

        // The edge lines carry a link that crosses the container's border, as the funnel's do.
        public Line Incoming(Node child)
        {
            var line = Active(new Line(Parent, child));
            line.Add(new Link(Outside, child));
            return line;
        }

        public Line Outgoing(Node child)
        {
            var line = Active(new Line(child, Parent));
            line.Add(new Link(child, Outside));
            return line;
        }

        public Line Sibling(Node source, Node target)
        {
            var line = Active(new Line(source, target));
            source.SourceLines.Add(line);
            target.TargetLines.Add(line);
            return line;
        }

        static Line Active(Line line)
        {
            line.IsActiveRep = true;
            return line;
        }
    }

    static XElement Render(Line line, bool isPath = false) =>
        XElement.Parse($"<svg>{LineSvg.GetLineSvg(line, new Pos(0, 0), 1, isPath: isPath)}</svg>");

    static XElement Visible(XElement svg) =>
        svg.Descendants("g").Single(g => ((string?)g.Attribute("class"))?.StartsWith("line-vis") == true);

    static XElement? Ring(XElement svg) =>
        svg.Descendants("circle").SingleOrDefault(c => (string?)c.Attribute("class") == LineSvg.StubRingClass);

    [Fact]
    public void IsStub_ShouldBeTheLinesBetweenAContainerAndItsChildren()
    {
        var scene = new Scene();

        Assert.True(LineStub.IsStub(scene.Incoming(scene.A), isOnPath: false));
        Assert.True(LineStub.IsStub(scene.Outgoing(scene.A), isOnPath: false));
        Assert.False(LineStub.IsStub(scene.Sibling(scene.A, scene.B), isOnPath: false));
        Assert.False(LineStub.IsStub(new Line(scene.Parent, scene.A, isDirect: true), isOnPath: false));
    }

    [Fact]
    public void IsStub_ShouldNotBeALine_WhoseLinksAllEndAtTheContainerItself()
    {
        var scene = new Scene();

        // "a" uses its own container, and the container uses "a": nothing crosses the border.
        var toContainer = new Line(scene.A, scene.Parent);
        toContainer.Add(new Link(scene.A, scene.Parent));
        var fromContainer = new Line(scene.Parent, scene.A);
        fromContainer.Add(new Link(scene.Parent, scene.A));

        Assert.False(LineStub.IsStub(toContainer, isOnPath: false));
        Assert.False(LineStub.IsStub(fromContainer, isOnPath: false));

        // One link out of the container is enough for the stub.
        toContainer.Add(new Link(scene.A, scene.Outside));
        Assert.True(LineStub.IsStub(toContainer, isOnPath: false));
    }

    [Fact]
    public void IsStub_ShouldLeaveALineInFull_WhenItIsOnThePathOrTheUserShapedIt()
    {
        var scene = new Scene();

        Assert.False(LineStub.IsStub(scene.Incoming(scene.A), isOnPath: true));

        var labelled = scene.Incoming(scene.A);
        labelled.SetLabel("reads");
        Assert.False(LineStub.IsStub(labelled, isOnPath: false));

        var routed = scene.Incoming(scene.A);
        routed.SetSegmentPoints([new Pos(100, 300)]);
        routed.IsSegmentsUserSet = true;
        Assert.False(LineStub.IsStub(routed, isOnPath: false));
    }

    [Fact]
    public void GetGeometry_ShouldDrawAnIncomingStubIntoTheChildsLeftSide()
    {
        var scene = new Scene();
        var anchor = new Pos(200, 450);

        var stub = LineStub.GetGeometry(scene.Incoming(scene.A), anchor, childrenZoom: 1);

        // Level, 18 units long, ending at the child, with the ring before its start.
        Assert.Equal(anchor, stub.End);
        Assert.Equal(new Pos(182, 450), stub.Start);
        Assert.Equal(450, stub.Ring.Y);
        Assert.Equal(stub.Start.X - stub.RingRadius, stub.Ring.X, 6);
        Assert.Equal(stub.Ring.X - stub.RingRadius, stub.HitStart.X, 6);
        Assert.Equal(anchor, stub.HitEnd);
    }

    [Fact]
    public void GetGeometry_ShouldDrawAnOutgoingStubOutOfTheChildsRightSide()
    {
        var scene = new Scene();
        var anchor = new Pos(300, 450);

        var stub = LineStub.GetGeometry(scene.Outgoing(scene.A), anchor, childrenZoom: 1);

        Assert.Equal(anchor, stub.Start);
        Assert.Equal(new Pos(318, 450), stub.End);
        Assert.Equal(stub.End.X + stub.RingRadius, stub.Ring.X, 6);
        Assert.Equal(anchor, stub.HitStart);
        Assert.Equal(stub.Ring.X + stub.RingRadius, stub.HitEnd.X, 6);
    }

    [Theory]
    [InlineData(1, 18)] // Its length in the container's units
    [InlineData(5, 22)] // Zoomed in: no longer than the limit, like an arrow head
    [InlineData(0.1, 5)] // Zoomed out: no shorter than the limit
    public void GetGeometry_ShouldKeepTheStubsLengthWithinLimits(double zoom, double expectedLength)
    {
        var scene = new Scene();

        var stub = LineStub.GetGeometry(scene.Outgoing(scene.A), new Pos(0, 0), zoom);

        Assert.Equal(expectedLength, stub.End.X - stub.Start.X, 6);
    }

    [Fact]
    public void GetGeometry_ShouldLeaveFacingStubsApart_AtTheLayoutsNodeDistance()
    {
        // The layout keeps 60 units between two nodes side by side.
        var scene = new Scene();
        foreach (var zoom in new[] { 0.3, 0.5, 1, 2, 4 })
        {
            var gap = 60 * zoom;
            var leaving = LineStub.GetGeometry(scene.Outgoing(scene.A), new Pos(0, 0), zoom);
            var entering = LineStub.GetGeometry(scene.Incoming(scene.B), new Pos(gap, 0), zoom);

            Assert.True(leaving.HitEnd.X < entering.HitStart.X, $"The stubs meet at zoom {zoom}");
        }
    }

    [Fact]
    public void GetGeometry_ShouldDrawAMemberRowsStubShorter()
    {
        var scene = new Scene(NodeType.MethodMember);

        var stub = LineStub.GetGeometry(scene.Incoming(scene.A), new Pos(0, 0), childrenZoom: 1);

        Assert.Equal(12, stub.End.X - stub.Start.X, 6);
    }

    [Fact]
    public void GetDirection_ShouldBeLevel_WhenNoSiblingLineRunsThere()
    {
        var scene = new Scene();
        var above = new Node("p.c", scene.Parent) { Boundary = new Rect(400, 0, 100, 100), Type = NodeType.ClassType };
        scene.Parent.AddChild(above);
        scene.Sibling(scene.A, above); // Leaves a's right side steeply upward

        Assert.Equal((1, 0), LineStub.GetDirection(scene.Outgoing(scene.A)));
        Assert.Equal((1, 0), LineStub.GetDirection(scene.Incoming(scene.A)));
    }

    [Fact]
    public void GetDirection_ShouldTurnAside_WhenASiblingLineLeavesLevelFromTheSameSide()
    {
        var scene = new Scene();
        scene.Sibling(scene.A, scene.B); // Level, from a's right side to b's left side

        var leaving = LineStub.GetDirection(scene.Outgoing(scene.A));
        var entering = LineStub.GetDirection(scene.Incoming(scene.B));

        // Turned upward (screen y grows downward), still pointing right.
        Assert.True(leaving.X > 0.8 && leaving.Y < -0.5, $"{leaving}");
        Assert.True(entering.X > 0.8 && entering.Y < -0.5, $"{entering}");
        // The other side of each node has no such line.
        Assert.Equal((1, 0), LineStub.GetDirection(scene.Incoming(scene.A)));
        Assert.Equal((1, 0), LineStub.GetDirection(scene.Outgoing(scene.B)));
    }

    [Fact]
    public void GetDirection_ShouldIgnoreSiblingLinesThatAreNotDrawn()
    {
        var scene = new Scene();
        scene.Sibling(scene.A, scene.B).IsActiveRep = false;

        Assert.Equal((1, 0), LineStub.GetDirection(scene.Outgoing(scene.A)));
    }

    [Fact]
    public void GetDirection_ShouldBeUpright_AtAnInheritanceEnd()
    {
        var scene = new Scene();
        var outside = new Node("x", scene.Parent.Parent);

        // A subtype outside the container inherits "a": the line enters a's bottom from below.
        var entering = new Line(scene.Parent, scene.A, isInheritance: true);
        entering.Add(new Link(outside, scene.A) { IsInheritance = true });
        Assert.Equal((0, -1), LineStub.GetDirection(entering));

        // "a" inherits a type outside the container: the line leaves a's top upward.
        var leaving = new Line(scene.A, scene.Parent, isInheritance: true);
        leaving.Add(new Link(scene.A, outside) { IsInheritance = true });
        Assert.Equal((0, -1), LineStub.GetDirection(leaving));
    }

    [Fact]
    public void RenderNodeLines_ShouldDrawStubsOnTopOfTheOtherLines()
    {
        var scene = new Scene();
        // "b" uses "a" at its left: that line starts at b's right side and runs back under "b"
        // and the stub into it, so the stub must be the later element to get the click.
        var incoming = scene.Incoming(scene.B);
        scene.Parent.SourceLines.Add(incoming);
        var back = scene.Sibling(scene.B, scene.A);
        var context = new RenderContext(Pos.None, 1, new Rect(0, 0, 1000, 1000), Pos.None, new ViewFlags());

        var svg = string.Concat(NodeLinesSvg.RenderNodeLines(scene.Parent, Pos.None, 1, context));

        var stubAt = svg.IndexOf(PointerId.FromLine(incoming.Id).ElementId, StringComparison.Ordinal);
        var backAt = svg.IndexOf(PointerId.FromLine(back.Id).ElementId, StringComparison.Ordinal);
        Assert.True(backAt >= 0 && stubAt > backAt, $"stub at {stubAt}, line at {backAt}");
    }

    [Fact]
    public void GetLineSvg_ShouldDrawAnIncomingStub_WithARingInPlaceOfTheStartCircle()
    {
        var scene = new Scene();
        var line = scene.Incoming(scene.A);

        var svg = Render(line);

        var visible = Visible(svg);
        Assert.Equal("182,450 200,450", (string?)visible.Element("polyline")!.Attribute("points"));
        var ring = Ring(svg);
        Assert.NotNull(ring);
        Assert.Equal("none", (string?)ring.Attribute("fill"));
        Assert.Same(visible, ring.Parent); // Dimmed, lit and softened with its line
        Assert.Single(visible.Elements("circle")); // The ring only: no filled start circle

        // Still the line's own element, and its hit target also covers the ring.
        var group = svg.Descendants("g").Single(g => (string?)g.Attribute("class") == "hoverable");
        Assert.Equal(PointerId.FromLine(line.Id).ElementId, (string?)group.Attribute("id"));
        var hit = group.Elements("polyline").Single(p => (string?)p.Attribute("class") == LineSvg.HitClass);
        Assert.Equal("177.32,450 200,450", (string?)hit.Attribute("points"));
    }

    [Fact]
    public void GetLineSvg_ShouldDrawAnOutgoingStub_FromTheChildsStartCircleToARing()
    {
        var scene = new Scene();

        var svg = Render(scene.Outgoing(scene.A));

        var visible = Visible(svg);
        Assert.Equal("300,450 318,450", (string?)visible.Element("polyline")!.Attribute("points"));
        var circles = visible.Elements("circle").ToList();
        Assert.Equal(2, circles.Count);
        var start = circles.Single(c => c.Attribute("class") is null);
        Assert.Equal("300", (string?)start.Attribute("cx"));
        Assert.Equal("320.34", (string?)Ring(svg)!.Attribute("cx"));
    }

    [Fact]
    public void GetLineSvg_ShouldDrawTheLineInFull_WhenItIsOnThePath()
    {
        var scene = new Scene();

        var svg = Render(scene.Incoming(scene.A), isPath: true);

        // From the container's left edge at mid height to the child.
        Assert.Equal("0,500 200,450", (string?)Visible(svg).Element("polyline")!.Attribute("points"));
        Assert.Null(Ring(svg));
    }

    [Fact]
    public void GetLineSvg_ShouldShowNoLinePointHandles_OnASelectedStub()
    {
        var scene = new Scene();
        var line = scene.Incoming(scene.A);
        line.SetSegmentPoints([new Pos(100, 300)]); // Routed by the layout, not by the user
        line.IsSelected = true;

        var svg = Render(line);

        Assert.NotNull(Ring(svg));
        Assert.DoesNotContain(svg.Descendants("g"), g => (string?)g.Attribute("class") == "selectpoint");
    }
}
