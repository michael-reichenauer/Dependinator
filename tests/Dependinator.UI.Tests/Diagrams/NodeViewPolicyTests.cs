using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;

namespace Dependinator.UI.Tests.Diagrams;

public class NodeViewPolicyTests
{
    [Theory]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public void ContainerBackgroundOpacity_ShouldBeFull_WhenContainerHasJustOpened(double zoom)
    {
        Assert.Equal(1.0, NodeViewPolicy.ContainerBackgroundOpacity(zoom));
    }

    [Theory]
    [InlineData(8.0, 0.75)]
    [InlineData(16.0, 0.5)]
    [InlineData(32.0, 0.25)]
    public void ContainerBackgroundOpacity_ShouldFadeInLogSpace_WhenZoomingIn(double zoom, double expected)
    {
        // Each doubling of the zoom removes the same share, so the fade feels even while zooming.
        Assert.Equal(expected, NodeViewPolicy.ContainerBackgroundOpacity(zoom), 3);
    }

    [Fact]
    public void ContainerBackgroundOpacity_ShouldReachZero_WhereTheChromeIsDropped()
    {
        // The fade ends exactly where IsTooLargeToBeSeen removes the rect, so the cut is seamless.
        Assert.Equal(0.0, NodeViewPolicy.ContainerBackgroundOpacity(64.0));
        Assert.Equal(0.0, NodeViewPolicy.ContainerBackgroundOpacity(1000.0));
        Assert.False(NodeViewPolicy.IsTooLargeToBeSeen(64.0));
        Assert.True(NodeViewPolicy.IsTooLargeToBeSeen(64.01));
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
        Assert.True(NodeViewPolicy.IsShowIcon(Enum.Parse<NodeType>(type), 100.0));
    }

    [Theory]
    [InlineData("Resource")]
    [InlineData("Module")]
    public void IsShowIcon_ShouldBeFalse_ForInfrastructureContainers_WhenZoomedIn(string type)
    {
        Assert.False(NodeViewPolicy.IsShowIcon(Enum.Parse<NodeType>(type), 100.0));
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
}
