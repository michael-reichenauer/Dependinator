using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;
using Rect = Dependinator.UI.Shared.Types.Rect;

namespace Dependinator.UI.Tests.Diagrams;

// The View › Rows filter leaves whole kinds of member rows out of the diagram together with
// their lines (even focus and direct lines: the row they would end at is not drawn).
public class SvgServiceRowFilterTests
{
    const double ViewSize = 2000;

    static Node AddNode(IModel model, string name, Node parent, NodeType type, Rect boundary)
    {
        var node = new Node(name, parent) { Type = type, Boundary = boundary };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static Line AddLink(IModel model, Node source, Node target)
    {
        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
        return model.Lines[LineId.From(source.Name, target.Name)];
    }

    static string RenderView(IModelMgr modelMgr, RowFilter rows)
    {
        var viewOptions = new ViewOptions();
        viewOptions.SetRowFilter(rows);
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

    [Fact]
    public void Hides_ShouldMapEachRowKindToItsFlag()
    {
        Assert.True(new RowFilter(true, false, false, false).Hides(NodeType.ParameterMember));
        Assert.True(new RowFilter(false, true, false, false).Hides(NodeType.VariableMember));
        Assert.True(new RowFilter(false, false, true, false).Hides(NodeType.OutputMember));
        Assert.True(new RowFilter(false, false, false, true).Hides(NodeType.ResourceMember));

        var all = new RowFilter(true, true, true, true);
        Assert.False(all.Hides(NodeType.MethodMember));
        Assert.False(all.Hides(NodeType.Resource));
        Assert.False(all.Hides(NodeType.Module));

        Assert.False(RowFilter.None.IsAny);
        Assert.True(new RowFilter(false, false, true, false).IsAny);
    }

    [Fact]
    public void IsLineFilteredOut_ShouldDropLinesToLeftOutRows_EvenFocusAndDirectLines()
    {
        var model = new ModelMgr(new StateMgr()).UseModel();
        var module = AddNode(model, "Module", model.Root, NodeType.Module, new Rect(0, 0, 100, 100));
        var other = AddNode(model, "Other", model.Root, NodeType.Module, new Rect(200, 0, 100, 100));
        var location = AddNode(model, "location", model.Root, NodeType.ParameterMember, new Rect(0, 200, 100, 20));

        var toRow = AddLink(model, module, location);
        var between = AddLink(model, module, other);
        var hideParameters = RowFilter.None with { HideParameters = true };

        Assert.True(SvgService.IsLineFilteredOut(toRow, LineFilter.None, hideParameters));
        Assert.False(SvgService.IsLineFilteredOut(toRow, LineFilter.None, RowFilter.None));
        Assert.False(SvgService.IsLineFilteredOut(between, LineFilter.None, hideParameters));

        toRow.IsFocused = true;
        Assert.True(SvgService.IsLineFilteredOut(toRow, LineFilter.None, hideParameters));
        var direct = new Line(module, location, isDirect: true);
        Assert.True(SvgService.IsLineFilteredOut(direct, LineFilter.None, hideParameters));
        Assert.False(SvgService.IsLineFilteredOut(direct, LineFilter.None, RowFilter.None));
    }

    [Fact]
    public void GetContentSvg_ShouldLeaveOutTheRowsAndTheirLines_WhenFiltered()
    {
        var modelMgr = new ModelMgr(new StateMgr());
        using (var model = modelMgr.UseModel())
        {
            var module = AddNode(model, "Module", model.Root, NodeType.Module, new Rect(50, 50, 100, 100));
            var location = AddNode(model, "location", model.Root, NodeType.ParameterMember, new Rect(300, 50, 100, 20));
            var prefix = AddNode(model, "prefix", model.Root, NodeType.VariableMember, new Rect(300, 100, 100, 20));
            AddLink(model, module, location);
            AddLink(model, module, prefix);
        }

        var shown = RenderView(modelMgr, RowFilter.None);
        Assert.Contains(">location</text>", shown);
        Assert.Contains(">prefix</text>", shown);
        Assert.Equal(2, CountOf(shown, "marker-end=\"url(#arrow-line)\""));

        var filtered = RenderView(modelMgr, RowFilter.None with { HideParameters = true });
        Assert.DoesNotContain(">location</text>", filtered);
        Assert.Contains(">prefix</text>", filtered);
        Assert.Contains(">Module</text>", filtered);
        Assert.Equal(1, CountOf(filtered, "marker-end=\"url(#arrow-line)\""));
    }

    static int CountOf(string text, string part) => (text.Length - text.Replace(part, "").Length) / part.Length;
}
