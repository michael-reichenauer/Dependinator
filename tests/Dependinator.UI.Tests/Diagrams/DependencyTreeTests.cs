using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Dependencies;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The explorer tree: one row per far node even when usage and inheritance segments are separate
// lines, and, with indirect ones included, the nodes reached through other nodes merged into the
// same containers with their hop counts, in both directions.
public class DependencyTreeTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

    static Node AddNode(IModel model, string name, Node parent, NodeType type = NodeType.ClassType)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100), Type = type };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static void AddLink(IModel model, Node source, Node target, bool isInheritance = false)
    {
        var link = new Link(source, target) { IsInheritance = isInheritance };
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
    }

    static IReadOnlyList<TreeItem> Expand(TreeItem item)
    {
        item.Expanded = true;
        return (item.Children ?? []).Cast<TreeItem>().ToList();
    }

    [Fact]
    public void ForNode_ShouldMergeUsageAndInheritanceLines_IntoOneRowPerFarNode()
    {
        using var model = modelMgr.UseModel();
        var a = AddNode(model, "A", model.Root, NodeType.Assembly);
        var b = AddNode(model, "B", model.Root, NodeType.Assembly);
        var x = AddNode(model, "A.X", a);
        var y = AddNode(model, "B.Y", b);
        var z = AddNode(model, "B.IZ", b, NodeType.InterfaceType);
        AddLink(model, x, y);
        AddLink(model, x, z, isInheritance: true);

        var items = DependencyTree.ForNode(model, x, TreeType.Dependencies, includeIndirect: false);

        var row = Assert.Single(items);
        Assert.Equal(b.Id, row.NodeId);
        Assert.Equal(2, row.LinkCount);
        Assert.False(row.IsIndirect);
        var children = Expand(row);
        Assert.Equal([y.Id, z.Id], children.Select(c => c.NodeId));
        Assert.All(children, c => Assert.Equal(1, c.LinkCount));
    }

    [Fact]
    public void ForNode_ShouldListIndirectNodes_InTheSameContainer_WithHopCounts()
    {
        using var model = modelMgr.UseModel();
        var a = AddNode(model, "A", model.Root, NodeType.Assembly);
        var b = AddNode(model, "B", model.Root, NodeType.Assembly);
        var x = AddNode(model, "A.X", a);
        var y = AddNode(model, "B.Y", b);
        var w = AddNode(model, "B.W", b);
        AddLink(model, x, y);
        AddLink(model, y, w); // W is two hops from X

        var direct = DependencyTree.ForNode(model, x, TreeType.Dependencies, includeIndirect: false);
        Assert.Equal([y.Id], Expand(Assert.Single(direct)).Select(c => c.NodeId));

        var withIndirect = DependencyTree.ForNode(model, x, TreeType.Dependencies, includeIndirect: true);
        var row = Assert.Single(withIndirect);
        Assert.Equal(1, row.LinkCount);
        var children = Expand(row);
        Assert.Equal([y.Id, w.Id], children.Select(c => c.NodeId));
        Assert.Equal(0, children[0].HopCount);
        Assert.Equal(2, children[1].HopCount);
        Assert.True(children[1].IsIndirect);
    }

    [Fact]
    public void ForNode_ShouldAddIndirectOnlyContainers_AfterTheDirectOnes()
    {
        using var model = modelMgr.UseModel();
        var a = AddNode(model, "A", model.Root, NodeType.Assembly);
        var b = AddNode(model, "B", model.Root, NodeType.Assembly);
        var c = AddNode(model, "C", model.Root, NodeType.Assembly);
        var x = AddNode(model, "A.X", a);
        var y = AddNode(model, "B.Y", b);
        var v = AddNode(model, "C.V", c);
        var xMethod = AddNode(model, "A.X.Run()", x, NodeType.MethodMember);
        AddLink(model, xMethod, y);
        AddLink(model, y, v);

        var items = DependencyTree.ForNode(model, x, TreeType.Dependencies, includeIndirect: true);

        Assert.Equal([b.Id, c.Id], items.Select(i => i.NodeId));
        var indirectContainer = items[1];
        Assert.True(indirectContainer.IsIndirect);
        Assert.Equal(0, indirectContainer.HopCount);
        var leaf = Assert.Single(Expand(indirectContainer));
        Assert.Equal(v.Id, leaf.NodeId);
        Assert.Equal(2, leaf.HopCount);
    }

    [Fact]
    public void ForNode_ShouldFollowTheReferencesDirection_ForIndirectOnes()
    {
        using var model = modelMgr.UseModel();
        var a = AddNode(model, "A", model.Root, NodeType.Assembly);
        var b = AddNode(model, "B", model.Root, NodeType.Assembly);
        var c = AddNode(model, "C", model.Root, NodeType.Assembly);
        var x = AddNode(model, "A.X", a);
        var y = AddNode(model, "B.Y", b);
        var v = AddNode(model, "C.V", c);
        AddLink(model, x, y);
        AddLink(model, y, v);

        var items = DependencyTree.ForNode(model, v, TreeType.References, includeIndirect: true);

        Assert.Equal([b.Id, a.Id], items.Select(i => i.NodeId));
        Assert.Equal(1, items[0].LinkCount);
        var leaf = Assert.Single(Expand(items[1]));
        Assert.Equal(x.Id, leaf.NodeId);
        Assert.Equal(2, leaf.HopCount);
    }

    [Fact]
    public void GetIndirectChains_ShouldSkipDirectOnes_AndTheSubjectsOwnAncestors()
    {
        using var model = modelMgr.UseModel();
        var a = AddNode(model, "A", model.Root, NodeType.Assembly);
        var x = AddNode(model, "A.X", a);
        var y = AddNode(model, "A.Y", a);
        var z = AddNode(model, "A.Z", a);
        AddLink(model, x, y);
        AddLink(model, y, z);
        AddLink(model, y, a); // A link up to the subject's own container is not a dependency to list

        var chains = DependencyTree.GetIndirectChains(model, x, isReferences: false);

        var chain = Assert.Single(chains);
        Assert.Equal([z], chain.FarChain);
        Assert.Equal(2, chain.Hops);
    }
}
