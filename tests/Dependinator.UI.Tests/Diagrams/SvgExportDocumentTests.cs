using System.Globalization;
using Dependinator.UI.Diagrams.Svg;

namespace Dependinator.UI.Tests.Diagrams;

public class SvgExportDocumentTests
{
    [Fact]
    public void Create_ShouldProduceStandaloneDocument()
    {
        var content = """<use href="#Interface" xlink:href="#Interface" x="10" y="10" width="20" height="20"/>""";

        var svg = SvgExportDocument.Create(content, 800, 600, "#FAFAFA");

        Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\"", svg);
        Assert.Contains("width=\"800\" height=\"600\"", svg);
        Assert.Contains("viewBox=\"0 0 800 600\"", svg);
        Assert.Contains("<rect width=\"100%\" height=\"100%\" fill=\"#FAFAFA\"/>", svg);
        Assert.Contains(content, svg);
        Assert.EndsWith("</svg>", svg);
    }

    [Fact]
    public void Create_ShouldIncludeArrowMarkersAndLinkHandleHiding()
    {
        var svg = SvgExportDocument.Create("", 100, 100, "#FFF");

        Assert.Contains("id=\"arrow-line\"", svg);
        Assert.Contains("id=\"arrow-hidden\"", svg);
        Assert.Contains("id=\"arrow-direct\"", svg);
        Assert.Contains("id=\"arrow-inheritance\"", svg);
        Assert.Contains(".linkhandle { display: none; }", svg);
    }

    [Fact]
    public void Create_ShouldIncludeOnlyUsedIconDefs()
    {
        var content = """<use href="#Interface" xlink:href="#Interface"/>""";

        var svg = SvgExportDocument.Create(content, 100, 100, "#FFF");

        Assert.Contains("id=\"Interface\"", svg);
        Assert.DoesNotContain("id=\"Solution\"", svg);
    }

    [Fact]
    public void Create_ShouldSkipUnknownIconReferences()
    {
        var content = """<use href="#NoSuchIcon" xlink:href="#NoSuchIcon"/>""";

        var svg = SvgExportDocument.Create(content, 100, 100, "#FFF");

        // The unknown reference is left unresolved rather than mapped to the Module fallback,
        // whose id would not match the reference anyway.
        Assert.DoesNotContain("id=\"Module\"", svg);
    }

    [Fact]
    public void Create_ShouldUseInvariantNumberFormat()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
            var svg = SvgExportDocument.Create("", 12.5, 20.25, "#FFF");

            Assert.Contains("width=\"12.5\" height=\"20.25\"", svg);
            Assert.Contains("viewBox=\"0 0 12.5 20.25\"", svg);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
