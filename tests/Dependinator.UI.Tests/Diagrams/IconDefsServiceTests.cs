using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Icons;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Node = Dependinator.UI.Modeling.Models.Node;
using Rect = Dependinator.UI.Shared.Types.Rect;

namespace Dependinator.UI.Tests.Diagrams;

// The canvas icon defs are filtered to the icons the current model actually references
// (instead of the whole library), recomputed on ModelChanged, and kept reference-stable
// while the used-icon set is unchanged so the Blazor diff can skip the defs DOM.
public class IconDefsServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly ApplicationEvents applicationEvents = new(Mock.Of<IJSInterop>());

    IconDefsService CreateService() => new(modelMgr, applicationEvents);

    void AddNode(string name, NodeType type)
    {
        using var model = modelMgr.UseModel();
        var node = new Node(name, model.Root) { Boundary = new Rect(0, 0, 100, 100), Type = type };
        model.Root.AddChild(node);
        model.TryAddNode(node);
    }

    [Fact]
    public void Defs_ShouldContainUsedIcons_AndNotUnusedOnes()
    {
        AddNode("A", NodeType.ClassType);
        var service = CreateService();

        var defs = service.Defs;

        Assert.Contains("id=\"Type\"", defs);
        Assert.DoesNotContain("id=\"Interface\"", defs);
    }

    [Fact]
    public void Defs_ShouldStayReferenceStable_WhenModelChangeDoesNotAlterIcons()
    {
        AddNode("A", NodeType.ClassType);
        var service = CreateService();
        var first = service.Defs;

        applicationEvents.TriggerModelChanged();

        Assert.Same(first, service.Defs);
    }

    [Fact]
    public void Defs_ShouldIncludeNewIcon_WhenModelChangeAddsOne()
    {
        AddNode("A", NodeType.ClassType);
        var service = CreateService();
        Assert.DoesNotContain("id=\"Interface\"", service.Defs);

        AddNode("B", NodeType.InterfaceType);
        applicationEvents.TriggerModelChanged();

        Assert.Contains("id=\"Interface\"", service.Defs);
        Assert.Contains("id=\"Type\"", service.Defs);
    }
}
