using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using ModelNode = Dependinator.UI.Modeling.Models.Node;
using ParsedNode = Dependinator.Core.Parsing.Node;

namespace Dependinator.UI.Tests.Models;

// The automatic layout sizes children by their weight among their siblings (NodeSizing).
public class NodeLayoutSizingTests
{
    [Fact]
    public void AdjustChildren_ShouldSizeChildrenByCodeMass()
    {
        using var model = NewModel();
        var parent = AddNode(model, model.Root, "Parent", NodeType.Namespace, new Rect(0, 0, 1200, 800));
        var tiny = AddNode(model, parent, "Parent.Tiny", lines: 5);
        var usual1 = AddNode(model, parent, "Parent.Usual1", lines: 16);
        var usual2 = AddNode(model, parent, "Parent.Usual2", lines: 16);
        var big = AddNode(model, parent, "Parent.Big", lines: 200);

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        Assert.Equal(new Size(60, 60), SizeOf(tiny));
        Assert.Equal(new Size(100, 100), SizeOf(usual1));
        Assert.Equal(new Size(100, 100), SizeOf(usual2));
        Assert.Equal(new Size(180, 180), SizeOf(big));
        AssertNoOverlaps(parent.Children);
    }

    [Fact]
    public void AdjustChildren_TypeWithMembers_ShouldSizeOnlyTheNestedTypes()
    {
        using var model = NewModel();
        var type = AddNode(model, model.Root, "Type", NodeType.ClassType, new Rect(0, 0, 900, 700));
        var method = AddNode(model, type, "Type.Method", lines: 1, type: NodeType.MethodMember);
        var small = AddNode(model, type, "Type.Small", lines: 4);
        var large = AddNode(model, type, "Type.Large", lines: 300);

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(type);

        Assert.Equal(18, method.Boundary.Height);
        Assert.True(SizeOf(large).Width > SizeOf(small).Width);
        AssertNoOverlaps(type.Children);
    }

    [Fact]
    public void AdjustChildren_ChildResized_ShouldRescaleItsContentFit()
    {
        // The child's own children were laid out for its old size; they keep their look
        using var model = NewModel();
        var parent = AddNode(model, model.Root, "Parent", NodeType.Namespace, new Rect(0, 0, 1200, 800));
        var big = AddNode(model, parent, "Parent.Big", lines: 200, boundary: new Rect(0, 0, 100, 100));
        AddNode(model, parent, "Parent.A", lines: 16);
        AddNode(model, parent, "Parent.B", lines: 16);
        AddNode(model, big, "Parent.Big.Inner", lines: 50, boundary: new Rect(10, 10, 100, 100));
        big.IsChildrenLayoutRequired = false;
        big.ContainerZoom = 0.2;
        big.ContainerOffset = new Pos(10, 20);

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        Assert.Equal(new Size(180, 180), SizeOf(big));
        Assert.Equal(0.36, big.ContainerZoom, 6);
        Assert.Equal(18, big.ContainerOffset.X, 6);
        Assert.Equal(36, big.ContainerOffset.Y, 6);
    }

    [Fact]
    public void AdjustChildren_ResizedNodeWithPassThroughChain_ShouldRescaleTheDeepestTransform()
    {
        // An assembly shows its namespace chain's content directly: the deepest pass-through
        // node holds the content transform, the assembly's own stays the identity
        using var model = NewModel();
        var parent = AddNode(model, model.Root, "Parent", NodeType.Namespace, new Rect(0, 0, 1200, 800));
        var assembly = AddNode(model, parent, "Parent.Big", lines: 200, boundary: new Rect(0, 0, 100, 100));
        AddNode(model, parent, "Parent.A", lines: 16);
        AddNode(model, parent, "Parent.B", lines: 16);
        assembly.ContainerZoom = 1;
        assembly.ContainerOffset = Pos.None;
        assembly.IsChildrenLayoutRequired = false;
        var chain = AddNode(model, assembly, "Parent.Big.Space", NodeType.Namespace, Rect.None);
        chain.IsPassThrough = true;
        chain.ContainerZoom = 0.2;
        chain.ContainerOffset = new Pos(10, 20);
        AddNode(model, chain, "Parent.Big.Space.Type", lines: 50, boundary: new Rect(10, 10, 100, 100));
        chain.IsChildrenLayoutRequired = false;

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        Assert.Equal(new Size(180, 180), SizeOf(assembly));
        Assert.Equal(1, assembly.ContainerZoom);
        Assert.Equal(Pos.None, assembly.ContainerOffset);
        Assert.Equal(0.36, chain.ContainerZoom, 6);
        Assert.Equal(18, chain.ContainerOffset.X, 6);
        Assert.Equal(36, chain.ContainerOffset.Y, 6);
    }

    [Fact]
    public void AdjustChildren_WhenCustomized_NewChild_ShouldGetItsSizeWithoutOverlapping()
    {
        using var model = NewModel();
        var parent = AddNode(model, model.Root, "Parent", NodeType.Namespace, new Rect(0, 0, 1200, 800));
        parent.IsChildrenLayoutCustomized = true;
        var a = AddNode(model, parent, "Parent.A", lines: 16, boundary: new Rect(100, 100, 100, 100));
        var b = AddNode(model, parent, "Parent.B", lines: 16, boundary: new Rect(280, 100, 100, 100));
        var added = AddNode(model, parent, "Parent.Added", lines: 400);

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.AdjustChildren(parent);

        Assert.Equal(new Rect(100, 100, 100, 100), a.Boundary);
        Assert.Equal(new Rect(280, 100, 100, 100), b.Boundary);
        Assert.Equal(new Size(180, 180), SizeOf(added));
        AssertNoOverlaps(parent.Children);
    }

    [Fact]
    public void RequireChildrenLayoutIfOlder_ShouldRedoOnlyTheAutomaticLayouts()
    {
        using var model = NewModel();
        var automatic = AddNode(model, model.Root, "Automatic", NodeType.Namespace, new Rect(0, 0, 100, 100));
        AddNode(model, automatic, "Automatic.A", boundary: new Rect(0, 0, 100, 100));
        var arranged = AddNode(model, model.Root, "Arranged", NodeType.Namespace, new Rect(200, 0, 100, 100));
        arranged.IsChildrenLayoutCustomized = true;
        AddNode(model, arranged, "Arranged.A", boundary: new Rect(0, 0, 100, 100));
        automatic.IsChildrenLayoutRequired = false;
        arranged.IsChildrenLayoutRequired = false;

        ModelService.RequireChildrenLayoutIfOlder(model, NodeLayout.CurrentLayoutVersion);
        Assert.False(automatic.IsChildrenLayoutRequired);

        ModelService.RequireChildrenLayoutIfOlder(model, 0);
        Assert.True(automatic.IsChildrenLayoutRequired);
        Assert.False(arranged.IsChildrenLayoutRequired);
    }

    [Fact]
    public void ToDto_ShouldKeepThatTheChildrenStillNeedALayout()
    {
        // A save before a container is first shown must not lose its pending layout
        var node = new ModelNode("Node", null!) { Type = NodeType.Namespace, IsChildrenLayoutRequired = true };

        var copy = new ModelNode("Node", null!);
        copy.SetFromDto(node.ToDto());

        Assert.True(copy.IsChildrenLayoutRequired);
    }

    static IModel NewModel() => new ModelMgr(new StateMgr()).UseModel();

    static ModelNode AddNode(
        IModel model,
        ModelNode parent,
        string name,
        NodeType type = NodeType.ClassType,
        Rect? boundary = null,
        int? lines = null
    )
    {
        var node = new ModelNode(name, parent) { Type = type, Boundary = boundary ?? Rect.None };
        if (lines is { } count)
            node.Update(new ParsedNode(name, new() { FileSpan = new FileSpan("File.cs", 10, 10 + count - 1) }));
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static Size SizeOf(ModelNode node) => new(node.Boundary.Width, node.Boundary.Height);

    static void AssertNoOverlaps(IReadOnlyList<ModelNode> nodes)
    {
        for (var first = 0; first < nodes.Count; first++)
        {
            for (var second = first + 1; second < nodes.Count; second++)
            {
                var a = nodes[first].Boundary;
                var b = nodes[second].Boundary;
                var overlaps =
                    a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
                Assert.False(overlaps, $"{nodes[first].Name} {a} overlaps {nodes[second].Name} {b}");
            }
        }
    }
}
