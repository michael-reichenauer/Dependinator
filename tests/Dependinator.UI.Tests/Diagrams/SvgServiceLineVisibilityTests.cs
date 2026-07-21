using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Rect = Dependinator.UI.Shared.Types.Rect;

namespace Dependinator.UI.Tests.Diagrams;

// Lines are rendered based on endpoint visibility, not zoom: a line is drawn while at least
// one of its endpoint nodes is rendered in the view, and dropped when both endpoints are
// outside it.
public class SvgServiceLineVisibilityTests
{
    // At this zoom the children of a top-level container render at 1024x — far beyond the
    // node chrome visibility limit (NodeViewPolicy.MaxNodeZoom = 64), which used to cull
    // all lines regardless of endpoint visibility.
    const double DeepZoom = 1.0 / 8192;

    // Pixel geometry at DeepZoom (root ContainerZoom is 1, so top-level P renders at 8192x
    // and its children at 1024x): P spans 0..819200 px, child A 0..102400 px, and child B
    // 614400..716800 px.
    const double ViewSize = 2000;

    static IModelMgr CreateModel(out Line line)
    {
        IModelMgr modelMgr = new ModelMgr(new StateMgr());
        using var model = modelMgr.UseModel();

        var parent = AddNode(model, "P", model.Root, new Rect(0, 0, 100, 100));
        var source = AddNode(model, "A", parent, new Rect(0, 0, 100, 100));
        var target = AddNode(model, "B", parent, new Rect(600, 600, 100, 100));

        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
        line = model.Lines[LineId.From("A", "B")];

        return modelMgr;
    }

    static Node AddNode(IModel model, string name, Node parent, Rect boundary)
    {
        var node = new Node(name, parent) { Boundary = boundary };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static string RenderView(IModelMgr modelMgr, double viewX, double viewY, double zoom = DeepZoom)
    {
        ISvgService service = new SvgService(modelMgr, Mock.Of<ITilesMgr>());
        var canvasRect = new Rect(viewX * zoom, viewY * zoom, ViewSize * zoom, ViewSize * zoom);
        return service.GetContentSvg(canvasRect, zoom);
    }

    [Fact]
    public void GetContentSvg_ShouldRenderLine_WhenOneEndpointIsVisibleAtDeepZoom()
    {
        var modelMgr = CreateModel(out Line _);

        // The view covers source A (0..102400 px) but not target B (614400..716800 px).
        var svg = RenderView(modelMgr, 0, 0);

        Assert.Contains("marker-end=\"url(#arrow-line)\"", svg);
    }

    [Fact]
    public void GetContentSvg_ShouldNotRenderLine_WhenNoEndpointIsVisible()
    {
        var modelMgr = CreateModel(out Line _);

        // The view lies inside P but covers neither A (0..102400 px) nor B (614400..716800 px).
        var svg = RenderView(modelMgr, 300000, 300000);

        Assert.DoesNotContain("marker-end", svg);
    }

    // At this zoom top-level containers are expanded and their children icons, so
    // RepLineService creates cousin (crossing) lines from the source child to the target's
    // top container (RenderAncestor = root).
    const double CousinZoom = 0.1;

    // ParentB lies 5000 root units (25 viewports at CousinZoom) to the right of ParentA, so
    // the cousin line's far end is well outside any view showing the source.
    static IModelMgr CreateCousinModel()
    {
        IModelMgr modelMgr = new ModelMgr(new StateMgr());
        using var model = modelMgr.UseModel();

        var parentA = AddNode(model, "ParentA", model.Root, new Rect(0, 0, 100, 100));
        var parentB = AddNode(model, "ParentB", model.Root, new Rect(5000, 0, 100, 100));
        var source = AddNode(model, "Source", parentA, new Rect(0, 0, 100, 100));
        var target = AddNode(model, "Target", parentB, new Rect(0, 0, 100, 100));

        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);

        return modelMgr;
    }

    [Fact]
    public void GetContentSvg_ShouldRenderCousinLine_WhenSourceIsVisible_AndTargetFarAway()
    {
        var modelMgr = CreateCousinModel();

        // The view covers ParentA/Source; ParentB is ~25 viewports away.
        var svg = RenderView(modelMgr, 0, 0, CousinZoom);

        Assert.Contains("marker-end=\"url(#arrow-cousin)\"", svg);
    }

    [Fact]
    public void GetContentSvg_ShouldNotRenderCousinLine_WhenNoEndpointIsVisible()
    {
        var modelMgr = CreateCousinModel();

        // The view lies between ParentA and ParentB; the cousin line crosses it, but
        // neither endpoint node is visible.
        var svg = RenderView(modelMgr, 20000, 0, CousinZoom);

        Assert.DoesNotContain("marker-end=\"url(#arrow-cousin)\"", svg);
    }

    [Fact]
    public void GetContentSvg_ShouldRenderHiddenLineAsHidden_WhenEndpointIsVisible()
    {
        var modelMgr = CreateModel(out Line line);
        line.IsHidden = true;

        // ShowHiddenNodes defaults to true, so the line renders in the hidden style, as before.
        var svg = RenderView(modelMgr, 0, 0);

        Assert.Contains("marker-end=\"url(#arrow-hidden)\"", svg);
        Assert.DoesNotContain("marker-end=\"url(#arrow-line)\"", svg);
    }
}
