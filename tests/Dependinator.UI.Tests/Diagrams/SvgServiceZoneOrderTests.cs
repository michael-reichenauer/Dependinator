using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Rect = Dependinator.UI.Shared.Types.Rect;

namespace Dependinator.UI.Tests.Diagrams;

// A zone is background: it is emitted before the sibling lines and nodes drawn over it, and a
// selected zone's outline after them so the siblings cannot cover it.
public class SvgServiceZoneOrderTests
{
    const double ViewSize = 2000;

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

    static string RenderView(IModelMgr modelMgr, ViewOptions viewOptions)
    {
        ISvgService service = new SvgService(
            modelMgr,
            Mock.Of<ITilesMgr>(),
            viewOptions,
            new CycleService(modelMgr),
            Mock.Of<IPathFinderService>(),
            Mock.Of<IRuleService>()
        );
        return service.GetContentSvg(new Rect(0, 0, ViewSize, ViewSize), 1.0);
    }

    static (IModelMgr ModelMgr, Node Zone) CreateModel()
    {
        var modelMgr = new ModelMgr(new StateMgr());
        Node zone;
        using (var model = modelMgr.UseModel())
        {
            zone = AddNode(model, "Edge", model.Root, new Rect(0, 0, 500, 300));
            zone.IsManual = true;
            zone.IsZone = true;
            var a = AddNode(model, "A", model.Root, new Rect(50, 50, 100, 100));
            var b = AddNode(model, "B", model.Root, new Rect(300, 50, 100, 100));
            AddLink(model, a, b);
        }
        return (modelMgr, zone);
    }

    [Fact]
    public void GetContentSvg_ShouldDrawTheZone_BeforeSiblingLinesAndNodes()
    {
        var (modelMgr, zone) = CreateModel();

        var svg = RenderView(modelMgr, new ViewOptions());

        var zoneIndex = svg.IndexOf($"id=\"{PointerId.FromNode(zone.Id).ElementId}\"", StringComparison.Ordinal);
        var lineIndex = svg.IndexOf("<polyline", StringComparison.Ordinal);
        var nodeIndex = svg.IndexOf(">A</text>", StringComparison.Ordinal);
        Assert.True(zoneIndex >= 0, "zone not rendered");
        Assert.True(lineIndex > zoneIndex, "the line must be drawn over the zone");
        Assert.True(nodeIndex > zoneIndex, "the nodes must be drawn over the zone");
    }

    [Fact]
    public void GetContentSvg_ShouldDrawASelectedZonesOutline_AfterTheSiblings()
    {
        var (modelMgr, zone) = CreateModel();
        zone.IsSelected = true;

        var svg = RenderView(modelMgr, new ViewOptions());

        var nodeIndex = svg.IndexOf(">B</text>", StringComparison.Ordinal);
        var outlineIndex = svg.LastIndexOf("stroke-dasharray=\"5,5\"", StringComparison.Ordinal);
        Assert.True(nodeIndex >= 0 && outlineIndex > nodeIndex, "the selection outline must be on top");
    }

    [Fact]
    public void GetContentSvg_ShouldSkipAHiddenZone_WhenHiddenNodesAreNotShown()
    {
        var (modelMgr, zone) = CreateModel();
        zone.SetHidden(true, isUserSet: true);
        zone.IsSelected = true;
        var viewOptions = new ViewOptions();
        viewOptions.SetShowHiddenNodes(false);

        var svg = RenderView(modelMgr, viewOptions);

        Assert.DoesNotContain(PointerId.FromNode(zone.Id).ElementId, svg);
        Assert.DoesNotContain("stroke-dasharray=\"5,5\"", svg);
        Assert.Contains(">A</text>", svg);
    }
}
