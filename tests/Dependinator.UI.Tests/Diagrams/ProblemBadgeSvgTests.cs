using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Shared;
using Rect = Dependinator.UI.Shared.Types.Rect;

namespace Dependinator.UI.Tests.Diagrams;

// A node's problem badge: the count on its top-right corner, red with errors among the problems
// and orange with only warnings, left out when there is nothing to show or it would be too small
// to read, and never in the way of pointer events.
public class ProblemBadgeSvgTests
{
    static readonly Rect NodeRect = new(10, 20, 100, 100);

    [Fact]
    public void ProblemBadgeSvg_ShouldBeEmpty_WithoutProblems()
    {
        Assert.Equal("", NodeSvg.ProblemBadgeSvg(null, NodeRect, 1));
        Assert.Equal("", NodeSvg.ProblemBadgeSvg(new NodeProblems(0, 0), NodeRect, 1));
    }

    [Fact]
    public void ProblemBadgeSvg_ShouldBeRed_WhenErrorsAreAmongTheProblems()
    {
        var svg = NodeSvg.ProblemBadgeSvg(new NodeProblems(2, 3), NodeRect, 1);

        Assert.Contains($"fill=\"{DColors.ProblemError}\"", svg);
        Assert.Contains(">5</text>", svg);
        Assert.Contains("cx=\"110\" cy=\"20\"", svg); // The top-right corner
        Assert.Contains("pointer-events=\"none\"", svg);
    }

    [Fact]
    public void ProblemBadgeSvg_ShouldBeOrange_WithOnlyWarnings_AndCapTheCount()
    {
        var svg = NodeSvg.ProblemBadgeSvg(new NodeProblems(0, 120), NodeRect, 1);

        Assert.Contains($"fill=\"{DColors.ProblemWarning}\"", svg);
        Assert.Contains(">99+</text>", svg);
    }

    [Fact]
    public void ProblemBadgeSvg_ShouldBeLeftOut_WhenTheNodeIsTooSmallToReadIt()
    {
        Assert.Equal("", NodeSvg.ProblemBadgeSvg(new NodeProblems(1, 0), new Rect(0, 0, 10, 10), 1));
    }
}
