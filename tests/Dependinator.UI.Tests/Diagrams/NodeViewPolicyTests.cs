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

    [Fact]
    public void IsShowIcon_LargeNode_ShouldOpenAtLowerZoom()
    {
        // A node twice the reference size opens when it is as large on screen as a default node
        // opening, i.e. at half the zoom; a small node stays an icon a little longer.
        var large = NodeOfSize(200);
        var small = NodeOfSize(60);

        Assert.True(NodeViewPolicy.IsShowIcon(large, 1.0));
        Assert.False(NodeViewPolicy.IsShowIcon(large, 1.01));
        Assert.True(NodeViewPolicy.IsShowIcon(small, 3.0));
        Assert.False(NodeViewPolicy.IsShowIcon(small, 3.4));
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
    [InlineData(180, 180, 1.0)]
    [InlineData(360, 120, 2.0)]
    public void FramingZoomFactor_ShouldFrameLargeNodesAtMost180Pixels(double width, double height, double expected)
    {
        var node = new ModelNode("Node", null!) { Type = NodeType.ClassType, Boundary = new Rect(0, 0, width, height) };

        Assert.Equal(expected, NodeViewPolicy.FramingZoomFactor(node), 6);
    }

    static ModelNode DefaultNode(NodeType type = NodeType.ClassType) => NodeOfSize(100, type);

    static ModelNode NodeOfSize(double size, NodeType type = NodeType.ClassType) =>
        new("Node", null!) { Type = type, Boundary = new Rect(0, 0, size, size) };
}
