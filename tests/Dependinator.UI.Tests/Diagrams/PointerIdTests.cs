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
}
