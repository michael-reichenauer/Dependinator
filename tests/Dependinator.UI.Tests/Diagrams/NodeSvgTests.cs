using Dependinator.UI.Diagrams.Svg;

namespace Dependinator.UI.Tests.Diagrams;

public class NodeSvgTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<summary></summary>")]
    public void GetDescriptionLines_ShouldReturnEmpty_WhenEmptyOrTagsOnly(string? description)
    {
        Assert.Empty(NodeSvg.GetDescriptionLines(description, 25, 7));
    }

    [Fact]
    public void GetDescriptionLines_ShouldStripXmlDocTags_AndCollapseWhitespace()
    {
        var lines = NodeSvg.GetDescriptionLines("<summary>\n  Parses\t code\n</summary>", 25, 7);

        Assert.Equal(["Parses code"], lines);
    }

    [Fact]
    public void GetDescriptionLines_ShouldWordWrap_AtMaxWidth()
    {
        var lines = NodeSvg.GetDescriptionLines("Parses the assemblies into a model", 25, 7);

        Assert.Equal(["Parses the assemblies", "into a model"], lines);
        Assert.All(lines, line => Assert.True(line.Length <= 25));
    }

    [Fact]
    public void GetDescriptionLines_ShouldTruncateWithEllipsis_WhenExceedingMaxLines()
    {
        // maxLines = 1 (member behaviour): keep the first line, mark it with an ellipsis.
        var lines = NodeSvg.GetDescriptionLines("Parses the assemblies into a model", 25, 1);

        Assert.Equal(["Parses the assemblies…"], lines);
    }

    [Fact]
    public void GetDescriptionLines_ShouldTrimLastLine_SoEllipsisFitsWidth()
    {
        // A single 30-char word hard-breaks; with maxLines = 1 the kept line is trimmed to
        // leave room for the ellipsis (24 chars + "…" == 25).
        var lines = NodeSvg.GetDescriptionLines(new string('x', 30), 25, 1);

        Assert.Single(lines);
        Assert.Equal(new string('x', 24) + "…", lines[0]);
    }

    [Fact]
    public void GetDescriptionLines_ShouldHardBreak_WordsLongerThanWidth()
    {
        var lines = NodeSvg.GetDescriptionLines(new string('x', 30), 25, 7);

        Assert.Equal([new string('x', 25), new string('x', 5)], lines);
    }

    [Fact]
    public void GetDescriptionLines_ShouldNotTruncate_WhenWithinMaxLines()
    {
        var lines = NodeSvg.GetDescriptionLines("Parses code", 25, 7);

        Assert.Equal(["Parses code"], lines);
    }

    [Fact]
    public void DescriptionWidthForNode_ShouldFloor_WhenNodeIsNarrow()
    {
        // A small icon node stays at the readable minimum width.
        Assert.Equal(25, NodeSvg.DescriptionWidthForNode(10, 6));
    }

    [Fact]
    public void DescriptionWidthForNode_ShouldScaleWithWidth_ForWiderNodes()
    {
        // fontSize 6 * 0.45 char width = 2.7 px/char; 135 px => 50 chars.
        Assert.Equal(50, NodeSvg.DescriptionWidthForNode(135, 6));
    }

    [Fact]
    public void DescriptionWidthForNode_ShouldCap_ForVeryWideNodes()
    {
        Assert.Equal(100, NodeSvg.DescriptionWidthForNode(100000, 6));
    }
}
