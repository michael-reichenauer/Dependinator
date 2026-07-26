using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Rect = Dependinator.UI.Shared.Types.Rect;

namespace Dependinator.UI.Tests.Diagrams;

// Nodes smaller than half a pixel in both dimensions are invisible and are skipped entirely
// (including their subtree). A node that is sub-pixel in only one dimension still renders,
// and lines whose endpoint is a skipped sub-pixel node still draw, so the converging line
// texture that conveys structure at far-out zoom is kept.
public class SvgServiceCullingTests
{
    const double ViewSize = 2000;

    static IModelMgr CreateModelMgr() => new ModelMgr(new StateMgr());

    static Node AddNode(IModel model, string name, Node parent, Rect boundary)
    {
        var node = new Node(name, parent) { Boundary = boundary };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static void AddLink(IModel model, Node source, Node target)
    {
        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
    }

    static string RenderView(IModelMgr modelMgr, double viewX, double viewY, double zoom)
    {
        ISvgService service = new SvgService(modelMgr, Mock.Of<ITilesMgr>());
        var canvasRect = new Rect(viewX * zoom, viewY * zoom, ViewSize * zoom, ViewSize * zoom);
        return service.GetContentSvg(canvasRect, zoom);
    }

    [Fact]
    public void GetContentSvg_ShouldSkipNode_WhenSubPixelInBothDimensions()
    {
        var modelMgr = CreateModelMgr();
        using (var model = modelMgr.UseModel())
        {
            AddNode(model, "BigNode", model.Root, new Rect(0, 0, 100, 100));
            AddNode(model, "TinyNode", model.Root, new Rect(600, 600, 0.2, 0.2));
        }

        // At zoom 1 BigNode renders at 100 px while TinyNode would be 0.2 px.
        var svg = RenderView(modelMgr, 0, 0, 1.0);

        Assert.Contains(">BigNode</text>", svg);
        Assert.DoesNotContain("TinyNode", svg);
    }

    [Fact]
    public void GetContentSvg_ShouldRenderNode_WhenSubPixelInOneDimensionOnly()
    {
        var modelMgr = CreateModelMgr();
        using (var model = modelMgr.UseModel())
        {
            AddNode(model, "FlatNode", model.Root, new Rect(0, 0, 100, 0.2));
        }

        // 100 px wide but 0.2 px tall: still visible as a hairline, so it must render.
        var svg = RenderView(modelMgr, 0, 0, 1.0);

        Assert.Contains("FlatNode", svg);
    }

    [Fact]
    public void GetContentSvg_ShouldRenderLine_WhenOnlyVisibleEndpointIsSubPixel()
    {
        var modelMgr = CreateModelMgr();
        using (var model = modelMgr.UseModel())
        {
            var source = AddNode(model, "FarSource", model.Root, new Rect(-5000, -5000, 100, 100));
            var target = AddNode(model, "TinyTarget", model.Root, new Rect(600, 600, 0.2, 0.2));
            AddLink(model, source, target);
        }

        // The view covers only TinyTarget, which is culled as sub-pixel; the line must still
        // draw since its endpoint is within the view (its title still names the target).
        var svg = RenderView(modelMgr, 0, 0, 1.0);

        Assert.DoesNotContain(">TinyTarget</text>", svg);
        Assert.Contains("marker-end", svg);
    }
}
