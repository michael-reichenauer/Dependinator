using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Shared.Types;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

public class NodeViewPolicyTests
{
    [Theory]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public void ContainerBackgroundOpacity_ShouldBeFull_WhenContainerHasJustOpened(double zoom)
    {
        Assert.Equal(1.0, NodeViewPolicy.ContainerBackgroundOpacity(DefaultNode(), zoom));
    }

    [Theory]
    [InlineData(8.0, 0.75)]
    [InlineData(16.0, 0.5)]
    [InlineData(32.0, 0.25)]
    public void ContainerBackgroundOpacity_ShouldFadeInLogSpace_WhenZoomingIn(double zoom, double expected)
    {
        // Each doubling of the zoom removes the same share, so the fade feels even while zooming.
        Assert.Equal(expected, NodeViewPolicy.ContainerBackgroundOpacity(DefaultNode(), zoom), 3);
    }

    [Fact]
    public void ContainerBackgroundOpacity_ShouldReachZero_WhereTheChromeIsDropped()
    {
        // The fade ends exactly where IsTooLargeToBeSeen removes the rect, so the cut is seamless.
        Assert.Equal(0.0, NodeViewPolicy.ContainerBackgroundOpacity(DefaultNode(), 64.0));
        Assert.Equal(0.0, NodeViewPolicy.ContainerBackgroundOpacity(DefaultNode(), 1000.0));
        Assert.False(NodeViewPolicy.IsTooLargeToBeSeen(DefaultNode(), 64.0));
        Assert.True(NodeViewPolicy.IsTooLargeToBeSeen(DefaultNode(), 64.01));
    }

    // NodeType is internal, so the theories take the enum member name.
    [Theory]
    [InlineData("ParameterMember")]
    [InlineData("VariableMember")]
    [InlineData("OutputMember")]
    [InlineData("ResourceMember")]
    [InlineData("MethodMember")]
    public void IsShowIcon_ShouldBeTrue_ForMemberTypes_AtAnyZoom(string type)
    {
        // Members are always leaf rows; they never open as containers however far one zooms in.
        Assert.True(NodeViewPolicy.IsShowIcon(DefaultNode(Enum.Parse<NodeType>(type)), 100.0));
    }

    [Theory]
    [InlineData("Resource")]
    [InlineData("Module")]
    public void IsShowIcon_ShouldBeFalse_ForInfrastructureContainers_WhenZoomedIn(string type)
    {
        Assert.False(NodeViewPolicy.IsShowIcon(DefaultNode(Enum.Parse<NodeType>(type)), 100.0));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1.0)]
    [InlineData(100.0)]
    public void IsContainerView_ShouldBeFalse_ForZonesAndNotes_AtAnyZoom(double modelZoom)
    {
        // Annotations are leaves however large they are on screen: nothing is placed inside a
        // zone, dropped onto it or opened from it.
        var root = new Dependinator.UI.Modeling.Models.Node("", null!) { Type = NodeType.Root, ContainerZoom = 1 };
        var zone = new Dependinator.UI.Modeling.Models.Node("Edge", root)
        {
            IsManual = true,
            IsZone = true,
            Boundary = new Dependinator.UI.Shared.Types.Rect(0, 0, 400, 300),
            ContainerZoom = 1,
        };
        var note = new Dependinator.UI.Modeling.Models.Node("1", root)
        {
            IsManual = true,
            IsNote = true,
            Boundary = new Dependinator.UI.Shared.Types.Rect(0, 0, 40, 40),
            ContainerZoom = 1,
        };
        var plain = new Dependinator.UI.Modeling.Models.Node("Box", root)
        {
            IsManual = true,
            Boundary = new Dependinator.UI.Shared.Types.Rect(0, 0, 400, 300),
            ContainerZoom = 1,
        };
        root.AddChild(zone);
        root.AddChild(note);
        root.AddChild(plain);

        Assert.False(NodeViewPolicy.IsContainerView(zone, modelZoom));
        Assert.False(NodeViewPolicy.IsContainerView(note, modelZoom));
        Assert.False(NodeViewPolicy.IsChildrenShown(zone, modelZoom));
        Assert.True(NodeViewPolicy.IsAnnotation(zone));
        Assert.True(NodeViewPolicy.IsAnnotation(note));
        Assert.False(NodeViewPolicy.IsAnnotation(plain));
    }

    [Theory]
    [InlineData(1.99, true)]
    [InlineData(2.0, true)]
    [InlineData(2.01, false)]
    public void IsShowIcon_DefaultSizeNode_ShouldKeepTheReferenceThreshold(double zoom, bool expected)
    {
        Assert.Equal(expected, NodeViewPolicy.IsShowIcon(DefaultNode(), zoom));
    }

    // Siblings open together: every size the layout gives a node (NodeSizing.SideSteps) opens at
    // the zoom a default-size node does, so a container's children are all icons or all open.
    [Theory]
    [InlineData(60)]
    [InlineData(100)]
    [InlineData(140)]
    [InlineData(180)]
    public void IsShowIcon_LayoutSizes_ShouldOpenAtTheSameZoom(double side)
    {
        var node = NodeOfSize(side);

        Assert.True(NodeViewPolicy.IsShowIcon(node, 2.0));
        Assert.False(NodeViewPolicy.IsShowIcon(node, 2.01));
        Assert.False(NodeViewPolicy.IsPastIcon(node, 2.0));
        Assert.True(NodeViewPolicy.IsPastIcon(node, 2.01));
    }

    [Fact]
    public void IsShowIcon_NodeResizedFarBeyondTheLayoutSizes_ShouldOpenByItsSizeOnScreen()
    {
        // A node enlarged by hand opens at the latest when it is 400 pixels on screen, instead of
        // staying an ever larger icon; a node shrunk by hand opens once it is 100 pixels.
        var huge = NodeOfSize(400);
        var tiny = NodeOfSize(25);

        Assert.True(NodeViewPolicy.IsShowIcon(huge, 1.0));
        Assert.False(NodeViewPolicy.IsShowIcon(huge, 1.01));
        Assert.True(NodeViewPolicy.IsShowIcon(tiny, 4.0));
        Assert.False(NodeViewPolicy.IsShowIcon(tiny, 4.01));
    }

    [Fact]
    public void IsShowIcon_NonSquareNode_ShouldOpenByItsShorterSide()
    {
        var wide = new ModelNode("Node", null!) { Type = NodeType.ClassType, Boundary = new Rect(0, 0, 600, 100) };

        Assert.True(NodeViewPolicy.IsShowIcon(wide, 2.0));
        Assert.False(NodeViewPolicy.IsShowIcon(wide, 2.01));
    }

    [Fact]
    public void ContainerBackgroundOpacity_LargeNode_ShouldStayFullForADoublingAfterItOpens()
    {
        // A 180 node opens at zoom 2 like its siblings, when it is 360 pixels on screen: its tint
        // is full until zoom 4, as a default node's is, not only until it is 400 pixels (zoom 2.2).
        var large = NodeOfSize(180);

        Assert.Equal(1.0, NodeViewPolicy.ContainerBackgroundOpacity(large, 2.01));
        Assert.Equal(1.0, NodeViewPolicy.ContainerBackgroundOpacity(large, 4.0));
        Assert.True(NodeViewPolicy.ContainerBackgroundOpacity(large, 4.5) < 1.0);
        Assert.Equal(0.0, NodeViewPolicy.ContainerBackgroundOpacity(large, 64.0 / 1.8));
    }

    [Fact]
    public void IsShowIcon_NodeNotLaidOut_ShouldUseTheReferenceThreshold()
    {
        var node = new ModelNode("Node", null!) { Type = NodeType.ClassType, Boundary = Rect.None };

        Assert.True(NodeViewPolicy.IsShowIcon(node, 2.0));
        Assert.False(NodeViewPolicy.IsShowIcon(node, 2.01));
    }

    [Fact]
    public void IsTooLargeToBeSeen_ShouldScaleWithSize()
    {
        Assert.True(NodeViewPolicy.IsTooLargeToBeSeen(NodeOfSize(200), 32.01));
        Assert.False(NodeViewPolicy.IsTooLargeToBeSeen(NodeOfSize(200), 32.0));
    }

    [Fact]
    public void EffectiveZoom_Root_ShouldIgnoreTheRootSize()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root, Boundary = new Rect(0, 0, 1000, 1000) };

        Assert.Equal(1.5, NodeViewPolicy.EffectiveZoom(root, 1.5));
    }

    [Theory]
    [InlineData(100, 100, 1.0)]
    [InlineData(150, 150, 1.0)]
    [InlineData(180, 180, 1.2)]
    [InlineData(360, 120, 2.4)]
    public void FramingZoomFactor_ShouldFrameLargeNodesAtMost150Pixels(double width, double height, double expected)
    {
        var node = new ModelNode("Node", null!) { Type = NodeType.ClassType, Boundary = new Rect(0, 0, width, height) };

        Assert.Equal(expected, NodeViewPolicy.FramingZoomFactor(node), 6);
    }

    // A type opens to its summary card first and to its member rows once they are readable: rows
    // are read at the children's zoom (the view zoom × the type's container zoom).
    [Theory]
    [InlineData(1.5, false)] // An icon
    [InlineData(3.0, true)] // Open, rows at 0.75: the summary card
    [InlineData(5.0, false)] // Rows at 1.25: the member card
    public void IsMemberSummary_ShouldShowBetweenTheIconAndReadableRows(double zoom, bool expected)
    {
        var type = DefaultNode();
        type.ContainerZoom = 0.25;
        type.AddChild(new ModelNode("Node.Method", type) { Type = NodeType.MethodMember });

        Assert.Equal(expected, NodeViewPolicy.IsMemberSummary(type, zoom));
        Assert.Equal(zoom < 5.0, NodeViewPolicy.IsShowIcon(type, zoom)); // The summary acts as an icon
    }

    [Fact]
    public void IsMemberSummary_ShouldBeFalse_ForAModuleWithResources()
    {
        // A Bicep module's resources are its content: it opens as a container right away.
        var module = DefaultNode(NodeType.Module);
        module.ContainerZoom = 0.25;
        module.AddChild(new ModelNode("Node.param", module) { Type = NodeType.ParameterMember });
        module.AddChild(new ModelNode("Node.storage", module) { Type = NodeType.Resource });

        Assert.False(NodeViewPolicy.IsMemberSummary(module, 3.0));
        Assert.False(NodeViewPolicy.IsShowIcon(module, 3.0));
    }

    [Fact]
    public void IsMemberSummary_ShouldBeFalse_ForATypeWithoutMembers()
    {
        var type = DefaultNode();
        type.ContainerZoom = 0.25;
        type.AddChild(new ModelNode("Node.Nested", type) { Type = NodeType.ClassType });

        Assert.False(NodeViewPolicy.IsMemberSummary(type, 3.0));
    }

    [Theory]
    [InlineData(180, 1.0)] // A small type: its card at natural scale
    [InlineData(100, 1.0)]
    [InlineData(180, 0.1)] // A large type: its card scaled down
    public void FramingZoomFactor_Member_ShouldShowReadableRowsInAnOpenParent(double size, double containerZoom)
    {
        var type = NodeOfSize(size);
        type.ContainerZoom = containerZoom;
        var member = new ModelNode("Node.Method", type) { Type = NodeType.MethodMember };
        type.AddChild(member);

        // The parent's zoom with the member framed (at 1 / factor pixels per unit), also in a tile
        // rendered 1.3 times either way.
        var zoom = 1 / (containerZoom * NodeViewPolicy.FramingZoomFactor(member));
        foreach (var tileZoom in new[] { zoom / 1.3, zoom, zoom * 1.3 })
        {
            Assert.False(NodeViewPolicy.IsShowIcon(type, tileZoom), $"Icon or summary at {tileZoom}");
            Assert.False(NodeViewPolicy.IsTooLargeToBeSeen(type, tileZoom), $"Zoomed past at {tileZoom}");
        }
    }

    [Fact]
    public void FramingZoomFactor_NestedTypeInATypeWithMembers_ShouldKeepTheParentsRowsReadable()
    {
        var type = NodeOfSize(1000);
        type.ContainerZoom = 0.5;
        type.AddChild(new ModelNode("Node.Method", type) { Type = NodeType.MethodMember });
        var nested = new ModelNode("Node.Nested", type)
        {
            Type = NodeType.ClassType,
            Boundary = new Rect(0, 0, 300, 300),
        };
        type.AddChild(nested);

        // The parent's zoom with the nested type framed: not its summary card, which hides it
        var zoom = 1 / (type.ContainerZoom * NodeViewPolicy.FramingZoomFactor(nested));
        Assert.False(NodeViewPolicy.IsMemberSummary(type, zoom / 1.3));
    }

    // A node that is large in its parent (a container zoom near 1) is framed closer than as an
    // icon when its parent would otherwise still be an icon itself, hiding the node.
    [Theory]
    [InlineData(100, 0.3)]
    [InlineData(100, 0.8)]
    [InlineData(180, 0.8)]
    [InlineData(180, 1.0)]
    [InlineData(400, 1.0)]
    public void FramingZoomFactor_ChildOfAnyParent_ShouldFrameItInAnOpenParent(double parentSize, double containerZoom)
    {
        var parent = NodeOfSize(parentSize, NodeType.Namespace);
        parent.ContainerZoom = containerZoom;
        var child = NodeOfSize(100);
        parent.AddChild(child);

        // The parent's zoom with the child framed (at 1 / factor pixels per unit), also in a tile,
        // which is rendered up to 1.3 times closer than the view.
        var zoom = 1 / (containerZoom * NodeViewPolicy.FramingZoomFactor(child));
        Assert.False(NodeViewPolicy.IsShowIcon(parent, zoom), $"Parent is an icon at {zoom}");
        Assert.False(NodeViewPolicy.IsShowIcon(parent, zoom * 1.3), $"Parent is an icon at {zoom * 1.3}");
    }

    [Fact]
    public void FramingZoomFactor_ChildInADefaultContainer_ShouldStillFrameItAsAnIcon()
    {
        var parent = NodeOfSize(180, NodeType.Namespace); // Default container zoom (1/8)
        var child = NodeOfSize(180);
        parent.AddChild(child);

        Assert.Equal(1.2, NodeViewPolicy.FramingZoomFactor(child), 6);
    }

    static ModelNode DefaultNode(NodeType type = NodeType.ClassType) => NodeOfSize(100, type);

    static ModelNode NodeOfSize(double size, NodeType type = NodeType.ClassType) =>
        new("Node", null!) { Type = type, Boundary = new Rect(0, 0, size, size) };
}
