using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Shared;

// The share link format round-trips model key, node name and view, tolerates missing or broken
// parts, and leaves addresses without link parameters alone.
public class ShareLinkTests
{
    [Fact]
    public void Build_ShouldEscapeTheNodeName_AndParseShouldReadItBack()
    {
        var link = ShareLink.Build("https://dependinator.com/", "demo", "Demo*UI*dll.Demo.UI.Main", null);

        Assert.StartsWith("https://dependinator.com/?m=demo&n=Demo%2AUI%2Adll.Demo.UI.Main", link);
        var target = ShareLink.Parse(link);
        Assert.NotNull(target);
        Assert.Equal("demo", target.ModelKey);
        Assert.Equal("Demo*UI*dll.Demo.UI.Main", target.NodeName);
        Assert.Null(target.View);
    }

    [Fact]
    public void Build_ShouldWriteTheViewInvariantly_AndParseShouldReadItBack()
    {
        var link = ShareLink.Build("http://localhost:5000", "abc", null, new ViewLink(-120.5, 33.25, 0.125));

        Assert.Equal("http://localhost:5000/?m=abc&v=-120.5,33.25,0.125", link);
        var target = ShareLink.Parse(link);
        Assert.NotNull(target);
        Assert.Equal(new ViewLink(-120.5, 33.25, 0.125), target.View);
        Assert.Null(target.NodeName);
    }

    [Fact]
    public void Build_ShouldKeepTheViewPrecise_DeepInsideContainers()
    {
        // Four container levels down the zoom is 1/8^4 of the overview's; the center is in canvas
        // units, where a pixel is then a fraction of a thousandth.
        var view = new ViewLink(1234.56789, -0.00042, 0.000152587890625);

        var target = ShareLink.Parse(ShareLink.Build("https://dependinator.com/", "abc", null, view));

        Assert.NotNull(target?.View);
        Assert.Equal(view.CenterX, target.View.Value.CenterX, 1e-6);
        Assert.Equal(view.CenterY, target.View.Value.CenterY, 1e-9);
        Assert.Equal(view.Zoom, target.View.Value.Zoom, 1e-12);
    }

    [Fact]
    public void Parse_ShouldDropAViewWithNonFiniteNumbers()
    {
        Assert.Null(ShareLink.Parse("https://dependinator.com/?m=demo&v=NaN,0,1")?.View);
        Assert.Null(ShareLink.Parse("https://dependinator.com/?m=demo&v=0,Infinity,1")?.View);
        Assert.Null(ShareLink.Parse("https://dependinator.com/?m=demo&v=0,0,NaN")?.View);
        Assert.Null(ShareLink.Parse("https://dependinator.com/?m=demo&v=0,0,Infinity")?.View);
        Assert.Equal("demo", ShareLink.Parse("https://dependinator.com/?m=demo&v=0,0,NaN")?.ModelKey);
    }

    [Fact]
    public void Parse_ShouldReturnNull_WithoutLinkParameters()
    {
        Assert.Null(ShareLink.Parse("https://dependinator.com/"));
        Assert.Null(ShareLink.Parse("https://dependinator.com/?other=1"));
        Assert.Null(ShareLink.Parse("not a uri"));
    }

    [Fact]
    public void Parse_ShouldDropABrokenView_ButKeepTheRest()
    {
        var target = ShareLink.Parse("https://dependinator.com/?n=A&v=1,2");

        Assert.NotNull(target);
        Assert.Equal("A", target.NodeName);
        Assert.Null(target.ModelKey);
        Assert.Null(target.View);
        Assert.Null(ShareLink.Parse("https://dependinator.com/?v=1,2,0")?.View);
    }

    [Fact]
    public void ModelKeyFor_ShouldBeDemoForTheDemoModel_AndTheCloudKeyOtherwise()
    {
        Assert.Equal("demo", ShareLink.ModelKeyFor(Dependinator.Core.Shared.DemoModel.Path));
        Assert.Equal(
            global::Shared.CloudModelPath.CreateKey("C:\\Code\\My App\\My.sln"),
            ShareLink.ModelKeyFor("C:\\Code\\My App\\My.sln")
        );
    }
}
