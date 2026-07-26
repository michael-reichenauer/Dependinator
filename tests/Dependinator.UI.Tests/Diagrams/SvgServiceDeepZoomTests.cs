using System.Globalization;
using System.Text.RegularExpressions;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Rect = Dependinator.UI.Shared.Types.Rect;

namespace Dependinator.UI.Tests.Diagrams;

// At deep zoom (navigating to a deeply nested class or member), ancestors far larger than the
// screen must not be emitted as nested svg viewports with multi-million-pixel offsets: browsers
// combine those offsets in single-precision floats, which displaces the whole subtree by
// millions of pixels and leaves the view blank. Such ancestors render flattened instead, so all
// emitted viewport coordinates stay float-safe.
public class SvgServiceDeepZoomTests
{
    const double ViewSize = 2000;

    // Beyond this magnitude a nested svg viewport coordinate visibly loses precision in the
    // browser's single-precision transform math.
    const double FloatSafeCoordinate = 1_000_000;

    static IModelMgr CreateDeepModel(out Node leaf)
    {
        IModelMgr modelMgr = new ModelMgr(new StateMgr());
        using var model = modelMgr.UseModel();

        // A six-deep chain, each node placed well into its parent's inner space, so ancestor
        // viewport offsets at leaf zoom reach tens of millions of pixels (each level scales
        // by 1/ContainerZoom = 8).
        var parent = model.Root;
        leaf = null!;
        for (int i = 1; i <= 6; i++)
        {
            leaf = AddNode(model, $"N{i}", parent, new Rect(600, 600, 100, 100));
            parent = leaf;
        }

        return modelMgr;
    }

    static Node AddNode(IModel model, string name, Node parent, Rect boundary)
    {
        var node = new Node(name, parent) { Boundary = boundary };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static string RenderNodeView(IModelMgr modelMgr, Node node)
    {
        // The view NavigationService pans to: centered on the node at its own scale.
        var (pos, zoom) = node.GetCenterPosAndZoom();
        var canvasRect = new Rect(
            pos.X - ViewSize / 2 * zoom,
            pos.Y - ViewSize / 2 * zoom,
            ViewSize * zoom,
            ViewSize * zoom
        );

        ISvgService service = new SvgService(modelMgr, Mock.Of<ITilesMgr>());
        return service.GetContentSvg(canvasRect, zoom);
    }

    [Fact]
    public void GetContentSvg_ShouldRenderTargetNode_WhenNavigatedToDeepNode()
    {
        var modelMgr = CreateDeepModel(out Node leaf);

        var svg = RenderNodeView(modelMgr, leaf);

        Assert.Contains(">N6</text>", svg);
    }

    [Fact]
    public void GetContentSvg_ShouldKeepViewportCoordinatesFloatSafe_WhenNavigatedToDeepNode()
    {
        var modelMgr = CreateDeepModel(out Node leaf);

        var svg = RenderNodeView(modelMgr, leaf);

        var viewportCoordinates = Regex
            .Matches(svg, """<svg x="(?<x>[^"]+)" y="(?<y>[^"]+)" width="(?<w>[^"]+)" """)
            .SelectMany(m => new[] { m.Groups["x"].Value, m.Groups["y"].Value, m.Groups["w"].Value })
            .Select(v => double.Parse(v, CultureInfo.InvariantCulture))
            .ToList();

        Assert.All(
            viewportCoordinates,
            c => Assert.True(Math.Abs(c) < FloatSafeCoordinate, $"Viewport coordinate {c} exceeds float-safe magnitude")
        );
    }
}
