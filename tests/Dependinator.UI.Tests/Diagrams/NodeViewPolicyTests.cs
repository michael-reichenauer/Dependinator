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
}
