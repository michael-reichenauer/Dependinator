using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Tests.Diagrams;

public class PointerIdTests
{
    [Fact]
    public void FromNodeLinkHandle_ShouldRoundTripThroughParse()
    {
        var nodeId = NodeId.FromName("MyNode");
        var pointerId = PointerId.FromNodeLinkHandle(nodeId);

        Assert.EndsWith(".lh", pointerId.ElementId);

        var parsed = PointerId.Parse(pointerId.ElementId);
        Assert.Equal(pointerId, parsed);
        Assert.Equal(nodeId, parsed.NodeId);
    }

    [Fact]
    public void Parse_LinkHandle_ShouldOnlyBeLinkHandle()
    {
        var parsed = PointerId.Parse("abc.lh");

        Assert.True(parsed.IsLinkHandle);
        Assert.False(parsed.IsNode);
        Assert.False(parsed.IsResize);
        Assert.False(parsed.IsLine);
        Assert.False(parsed.IsLinePoint);
    }

    [Fact]
    public void Parse_Node_ShouldNotBeLinkHandle()
    {
        var parsed = PointerId.Parse("abc.n");

        Assert.True(parsed.IsNode);
        Assert.False(parsed.IsLinkHandle);
    }

    [Fact]
    public void Parse_LineLabel_ShouldBeTheLine_MarkedAsItsLabel()
    {
        var lineId = LineId.FromId("abc");

        var parsed = PointerId.Parse(PointerId.LineLabelElementId(lineId));

        Assert.True(parsed.IsLineLabel);
        Assert.True(parsed.IsLine);
        Assert.Equal(lineId, parsed.LineId);
        // The same target as the line itself: selecting, menus and hover cards treat it so.
        Assert.Equal(PointerId.FromLine(lineId), parsed);
        Assert.Equal(PointerId.FromLine(lineId).GetHashCode(), parsed.GetHashCode());
        Assert.Equal(PointerId.FromLine(lineId).ElementId, parsed.ElementId);
        Assert.False(PointerId.FromLine(lineId).IsLineLabel);
    }

    [Fact]
    public void FromLineMidPoint_ShouldRoundTripThroughParse_WithItsSegmentAndPlace()
    {
        var lineId = LineId.FromId("abc");
        var pointerId = PointerId.FromLineMidPoint(lineId, 2, 25);

        var parsed = PointerId.Parse(pointerId.ElementId);

        Assert.Equal(pointerId, parsed);
        Assert.True(parsed.IsLineMidPoint);
        Assert.Equal(2, parsed.LineMidPointIndex);
        Assert.Equal(0.25, parsed.LineMidPointFraction);
        Assert.Equal(lineId, parsed.LineId);
        Assert.True(parsed.IsLine); // it lies on the line: menus and hover cards are the line's
        Assert.False(parsed.IsLinePoint);
        Assert.NotEqual(PointerId.FromLineMidPoint(lineId, 2), parsed);
    }

    [Theory]
    [InlineData("abc.lm")]
    [InlineData("abc.lm2")]
    [InlineData("abc.lm2_0")]
    [InlineData("abc.lmx_50")]
    public void Parse_ShouldNotReadAMalformedSegmentHandle(string elementId)
    {
        Assert.False(PointerId.Parse(elementId).IsLineMidPoint);
    }
}
