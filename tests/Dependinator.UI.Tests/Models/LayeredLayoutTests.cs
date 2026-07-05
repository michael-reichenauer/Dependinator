using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

public class LayeredLayoutTests
{
    [Fact]
    public void AdjustChildren_DependencyChain_ShouldPlaceNodesInSuccessiveColumns()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddParent(model, new Rect(0, 0, 1200, 400));
        var a = AddChild(model, parent, "A");
        var b = AddChild(model, parent, "B");
        var c = AddChild(model, parent, "C");
        AddLink(model, a, b);
        AddLink(model, b, c);

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        Assert.True(a.Boundary.X < b.Boundary.X, $"Expected A left of B but got {a.Boundary.X} >= {b.Boundary.X}");
        Assert.True(b.Boundary.X < c.Boundary.X, $"Expected B left of C but got {b.Boundary.X} >= {c.Boundary.X}");
        Assert.All(parent.Children, child => Assert.NotEqual(Rect.None, child.Boundary));
    }

    [Fact]
    public void AdjustChildren_DependencyCycle_ShouldTerminateAndPlaceAllNodes()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddParent(model, new Rect(0, 0, 1200, 400));
        var a = AddChild(model, parent, "A");
        var b = AddChild(model, parent, "B");
        var c = AddChild(model, parent, "C");
        AddLink(model, a, b);
        AddLink(model, b, c);
        AddLink(model, c, a);

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        Assert.All(parent.Children, child => Assert.NotEqual(Rect.None, child.Boundary));
        Assert.True(parent.Children.Select(child => (child.Boundary.X, child.Boundary.Y)).Distinct().Count() == 3);
    }

    [Fact]
    public void AdjustChildren_ExternallyReferencedNode_ShouldBeInLeftmostColumnAndTop()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddParent(model, new Rect(0, 0, 1200, 400));
        var external = new ModelNode("External", model.Root) { Type = NodeType.Type };
        model.Root.AddChild(external);
        model.TryAddNode(external);

        var a = AddChild(model, parent, "A");
        var entry = AddChild(model, parent, "Entry");
        var b = AddChild(model, parent, "B");
        AddLink(model, a, entry); // Internal predecessor would normally push Entry rightward
        AddLink(model, entry, b);
        AddLink(model, external, entry); // External reference pins Entry to the left

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        var minX = parent.Children.Min(child => child.Boundary.X);
        Assert.Equal(minX, entry.Boundary.X);
        Assert.True(
            b.Boundary.X > entry.Boundary.X,
            $"Expected B right of Entry but got {b.Boundary.X} <= {entry.Boundary.X}"
        );
        Assert.True(
            entry.Boundary.Y <= a.Boundary.Y,
            $"Expected Entry at top but got {entry.Boundary.Y} > {a.Boundary.Y}"
        );
    }

    [Fact]
    public void AdjustChildren_IsolatedNodes_ShouldBePlacedBottomRight()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddParent(model, new Rect(0, 0, 1200, 400));
        var a = AddChild(model, parent, "A");
        var b = AddChild(model, parent, "B");
        var isolated = AddChild(model, parent, "Isolated");
        AddLink(model, a, b);

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        Assert.True(
            isolated.Boundary.X > b.Boundary.X,
            $"Expected isolated right of B but got {isolated.Boundary.X} <= {b.Boundary.X}"
        );
        var contentBottom = new[] { a, b }.Max(node => node.Boundary.Y + node.Boundary.Height);
        var isolatedBottom = isolated.Boundary.Y + isolated.Boundary.Height;
        Assert.True(
            Math.Abs(isolatedBottom - contentBottom) <= NodeGrid.SnapSize,
            $"Expected isolated aligned to content bottom {contentBottom} but got {isolatedBottom}"
        );
    }

    [Fact]
    public void AdjustChildren_CrossingEdges_ShouldReorderToReduceCrossings()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddParent(model, new Rect(0, 0, 1200, 400));
        var a = AddChild(model, parent, "A");
        var b = AddChild(model, parent, "B");
        var c = AddChild(model, parent, "C");
        var d = AddChild(model, parent, "D");
        // Name order (A,B) x (C,D) would cross: A->D and B->C
        AddLink(model, a, d);
        AddLink(model, b, c);

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        Assert.True(a.Boundary.X < d.Boundary.X);
        Assert.True(b.Boundary.X < c.Boundary.X);
        var sourcesTopToBottom = a.Boundary.Y < b.Boundary.Y;
        var targetsTopToBottom = d.Boundary.Y < c.Boundary.Y;
        Assert.True(
            sourcesTopToBottom == targetsTopToBottom,
            "Expected targets ordered to match sources to avoid a crossing"
        );
    }

    [Fact]
    public void AdjustChildren_LongChain_ShouldFoldToMatchParentAspect()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddParent(model, new Rect(0, 0, 1000, 1000));
        var children = Enumerable.Range(0, 8).Select(i => AddChild(model, parent, $"Node{i}")).ToList();
        for (var i = 0; i < children.Count - 1; i++)
        {
            AddLink(model, children[i], children[i + 1]);
        }

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        var columnCount = children.Select(child => child.Boundary.X).Distinct().Count();
        Assert.True(columnCount < 8, $"Expected folded layout but got {columnCount} columns for a chain of 8");
        Assert.True(columnCount > 1, "Expected more than one column");

        var bounds = GetBounds(children);
        var aspect = bounds.Width / bounds.Height;
        Assert.True(aspect < 4.0, $"Expected roughly square layout but aspect was {aspect:0.##}");
    }

    [Fact]
    public void AdjustChildren_ShuffledInsertionOrder_ShouldProduceSameLayout()
    {
        var first = LayoutWithInsertionOrder([0, 1, 2, 3, 4]);
        var second = LayoutWithInsertionOrder([4, 2, 0, 3, 1]);

        foreach (var (name, boundary) in first)
        {
            Assert.Equal(boundary, second[name]);
        }
    }

    [Fact]
    public void TryArrange_NoRelations_ShouldReturnFalseForGridFallback()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddParent(model, new Rect(0, 0, 1200, 400));
        AddChild(model, parent, "A");
        AddChild(model, parent, "B");

        var arranged = LayeredLayout.TryArrange(parent, new LayoutMetrics(100, 100, 70, 70, 1.0));

        Assert.False(arranged);
    }

    [Fact]
    public void AdjustChildren_CustomizedParentWithLinks_ShouldOnlyPlaceNewChildren()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddParent(model, new Rect(0, 0, 1200, 400));
        parent.IsChildrenLayoutCustomized = true;

        var a = AddChild(model, parent, "A");
        var b = AddChild(model, parent, "B");
        a.Boundary = new Rect(100, 100, 100, 100);
        b.Boundary = new Rect(400, 300, 100, 100);
        AddLink(model, a, b);

        var added = AddChild(model, parent, "C");
        AddLink(model, added, a);

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        Assert.Equal(new Rect(100, 100, 100, 100), a.Boundary);
        Assert.Equal(new Rect(400, 300, 100, 100), b.Boundary);
        Assert.NotEqual(Rect.None, added.Boundary);
    }

    [Fact]
    public void RequireChildrenLayout_ShouldFlagOnlyNonCustomizedParents()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddParent(model, new Rect(0, 0, 1200, 400));
        var a = AddChild(model, parent, "A");
        AddChild(model, parent, "B");

        var customized = new ModelNode("Customized", model.Root)
        {
            Type = NodeType.Namespace,
            Boundary = new Rect(0, 0, 400, 400),
            IsChildrenLayoutCustomized = true,
        };
        model.Root.AddChild(customized);
        model.TryAddNode(customized);
        AddChild(model, customized, "C");

        parent.IsChildrenLayoutRequired = false;
        customized.IsChildrenLayoutRequired = false;

        ModelService.RequireChildrenLayout(model);

        Assert.True(parent.IsChildrenLayoutRequired);
        Assert.True(model.Root.IsChildrenLayoutRequired);
        Assert.False(customized.IsChildrenLayoutRequired);
        Assert.False(a.IsChildrenLayoutRequired);
    }

    static Dictionary<string, Rect> LayoutWithInsertionOrder(int[] order)
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddParent(model, new Rect(0, 0, 1200, 400));

        var children = new ModelNode[5];
        foreach (var index in order)
        {
            children[index] = AddChild(model, parent, $"Node{index}");
        }

        // Diamond with a tail: 0 -> 1, 0 -> 2, 1 -> 3, 2 -> 3, 3 -> 4
        var links = new (int, int)[] { (0, 1), (0, 2), (1, 3), (2, 3), (3, 4) };
        foreach (var (source, target) in links)
        {
            AddLink(model, children[source], children[target]);
        }

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        return parent.Children.ToDictionary(child => child.Name, child => child.Boundary);
    }

    static ModelNode AddParent(IModel model, Rect boundary)
    {
        var parent = new ModelNode("Parent", model.Root) { Type = NodeType.Namespace, Boundary = boundary };
        model.Root.AddChild(parent);
        model.TryAddNode(parent);
        return parent;
    }

    static ModelNode AddChild(IModel model, ModelNode parent, string name)
    {
        var child = new ModelNode(name, parent) { Type = NodeType.Type };
        parent.AddChild(child);
        model.TryAddNode(child);
        return child;
    }

    static void AddLink(IModel model, ModelNode source, ModelNode target)
    {
        var link = new Link(source, target);
        new LineService().AddLinesFromSourceToTarget(model, link);
    }

    static Rect GetBounds(IReadOnlyList<ModelNode> nodes)
    {
        var minX = nodes.Min(node => node.Boundary.X);
        var minY = nodes.Min(node => node.Boundary.Y);
        var maxX = nodes.Max(node => node.Boundary.X + node.Boundary.Width);
        var maxY = nodes.Max(node => node.Boundary.Y + node.Boundary.Height);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
}
