using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Shared;
using Rect = Dependinator.UI.Shared.Types.Rect;

namespace Dependinator.UI.Tests.Diagrams;

// The badge on a node with children the user hid: the count on its top-left corner (the problem
// badge has the top-right), left out when nothing is hidden, and never in the way of pointer
// events.
public class HiddenBadgeSvgTests
{
    static readonly Rect NodeRect = new(10, 20, 100, 100);

    [Fact]
    public void HiddenBadgeSvg_ShouldBeEmpty_WhenNoChildIsHidden()
    {
        Assert.Equal("", NodeSvg.HiddenBadgeSvg(0, NodeRect, 1));
    }

    [Fact]
    public void HiddenBadgeSvg_ShouldShowTheCount_OnTheTopLeftCorner()
    {
        var svg = NodeSvg.HiddenBadgeSvg(3, NodeRect, 1);

        Assert.Contains($"fill=\"{DColors.HiddenBadge}\"", svg);
        Assert.Contains(">3</text>", svg);
        Assert.Contains("cx=\"10\" cy=\"20\"", svg);
        Assert.Contains("pointer-events=\"none\"", svg);
    }

    [Fact]
    public void HiddenBadgeSvg_ShouldBeLeftOut_WhenTooSmallToRead()
    {
        Assert.Equal("", NodeSvg.HiddenBadgeSvg(3, new Rect(0, 0, 10, 10), 1));
    }
}
