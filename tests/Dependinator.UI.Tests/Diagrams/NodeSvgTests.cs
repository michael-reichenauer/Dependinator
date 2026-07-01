using Dependinator.UI.Diagrams.Svg;

namespace Dependinator.UI.Tests.Diagrams;

public class NodeSvgTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<summary></summary>")]
    public void GetShortHtmlDescription_ShouldReturnNull_WhenEmptyOrTagsOnly(string? description)
    {
        Assert.Null(NodeSvg.GetShortHtmlDescription(description));
    }

    [Fact]
    public void GetShortHtmlDescription_ShouldStripXmlDocTags_AndCollapseWhitespace()
    {
        var result = NodeSvg.GetShortHtmlDescription("<summary>\n  Parses\t code\n</summary>");

        Assert.Equal("Parses code", result);
    }

    [Fact]
    public void GetShortHtmlDescription_ShouldTruncateWithEllipsis_WhenLongerThanLimit()
    {
        // 15 visible chars kept, then an ellipsis.
        var result = NodeSvg.GetShortHtmlDescription("Parses the assemblies into a model");

        Assert.Equal("Parses the asse…", result);
    }

    [Fact]
    public void GetShortHtmlDescription_ShouldNotTruncate_WhenWithinLimit()
    {
        Assert.Equal("Parses code", NodeSvg.GetShortHtmlDescription("Parses code"));
    }

    [Fact]
    public void GetShortHtmlDescription_ShouldHtmlEncode_SpecialCharacters()
    {
        var result = NodeSvg.GetShortHtmlDescription("a & b");

        Assert.Equal("a &amp; b", result);
    }
}
