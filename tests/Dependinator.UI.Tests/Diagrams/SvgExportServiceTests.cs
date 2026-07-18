using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using ModelNode = Dependinator.UI.Modeling.Models.Node;
using Rect = Dependinator.UI.Shared.Types.Rect;

namespace Dependinator.UI.Tests.Diagrams;

public class SvgExportServiceTests
{
    static IModelMgr CreateModelWithNode()
    {
        IModelMgr modelMgr = new ModelMgr(new StateMgr());
        using var model = modelMgr.UseModel();

        var node = new ModelNode("Node", model.Root)
        {
            Type = NodeType.InterfaceType,
            Boundary = new Rect(10, 10, 100, 50),
        };
        model.Root.AddChild(node);
        model.TryAddNode(node);
        return modelMgr;
    }

    static SvgExportService CreateService(IModelMgr modelMgr) => new(new SvgService(modelMgr, Mock.Of<ITilesMgr>()));

    [Fact]
    public void GetSvgDocument_ShouldSizeDocumentFromRectAndZoom()
    {
        var service = CreateService(CreateModelWithNode());

        var svg = service.GetSvgDocument(new Rect(0, 0, 1000, 500), 2);

        Assert.Contains("width=\"500\" height=\"250\"", svg);
        Assert.Contains("viewBox=\"0 0 500 250\"", svg);
    }

    [Fact]
    public void GetSvgDocument_ShouldContainNodeContentAndItsIconDef()
    {
        var service = CreateService(CreateModelWithNode());

        var svg = service.GetSvgDocument(new Rect(0, 0, 1000, 500), 1);

        Assert.Contains("href=\"#Interface\"", svg);
        Assert.Contains("id=\"Interface\"", svg);
    }

    [Fact]
    public void GetSvgDocument_ShouldExcludeNodesOutsideRect()
    {
        var service = CreateService(CreateModelWithNode());

        // The node's boundary (10,10,100,50) is entirely outside this rect.
        var svg = service.GetSvgDocument(new Rect(500, 500, 100, 100), 1);

        Assert.DoesNotContain("href=\"#Interface\"", svg);
    }

    [Fact]
    public void GetSvgDocument_ShouldReturnEmptyContentDocument_WhenModelIsEmpty()
    {
        var service = CreateService(new ModelMgr(new StateMgr()));

        var svg = service.GetSvgDocument(new Rect(0, 0, 100, 100), 1);

        Assert.Contains("width=\"100\" height=\"100\"", svg);
        Assert.DoesNotContain("<use", svg);
    }
}
