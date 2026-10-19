using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Link = Dependinator.UI.Modeling.Models.Link;
using ModelNode = Dependinator.UI.Modeling.Models.Node;
using ParsedNode = Dependinator.Core.Parsing.Node;

namespace Dependinator.UI.Tests.Models;

public class NodeSizingTests
{
    [Fact]
    public void ComputeSides_NoCodeAndNoLinks_ShouldKeepTheDefaultSize()
    {
        using var model = NewModel();
        var parent = AddNode(model, model.Root, "Parent");
        var children = Enumerable.Range(0, 3).Select(index => AddManualNode(model, parent, $"M{index}")).ToList();

        var sides = NodeSizing.ComputeSides(children, parent);

        Assert.All(children, child => Assert.Equal(NodeSizing.DefaultSide, sides[child]));
    }

    [Fact]
    public void ComputeSides_ShouldScaleWithCodeMass()
    {
        using var model = NewModel();
        var parent = AddNode(model, model.Root, "Parent");
        var tiny = AddNode(model, parent, "Tiny", lines: 5);
        var usual1 = AddNode(model, parent, "Usual1", lines: 16);
        var usual2 = AddNode(model, parent, "Usual2", lines: 16);
        var big = AddNode(model, parent, "Big", lines: 200);

        var sides = NodeSizing.ComputeSides([tiny, usual1, usual2, big], parent);

        Assert.Equal(60, sides[tiny]);
        Assert.Equal(100, sides[usual1]);
        Assert.Equal(100, sides[usual2]);
        Assert.Equal(180, sides[big]);
    }

    [Fact]
    public void ComputeSides_SingleChild_ShouldBeDefault()
    {
        using var model = NewModel();
        var parent = AddNode(model, model.Root, "Parent");
        var only = AddNode(model, parent, "Only", lines: 5000);

        var sides = NodeSizing.ComputeSides([only], parent);

        Assert.Equal(NodeSizing.DefaultSide, sides[only]);
    }

    [Fact]
    public void ComputeSides_ShouldOnlyUseTheSizeSteps()
    {
        using var model = NewModel();
        var parent = AddNode(model, model.Root, "Parent");
        var children = Enumerable
            .Range(0, 13)
            .Select(index => AddNode(model, parent, $"N{index}", lines: 1 << index))
            .ToList();

        var sides = NodeSizing.ComputeSides(children, parent);

        Assert.All(sides.Values, side => Assert.Contains(side, NodeSizing.SideSteps));
        Assert.Equal(NodeSizing.SideSteps.Min(), sides.Values.Min());
        Assert.Equal(NodeSizing.SideSteps.Max(), sides.Values.Max());
    }

    [Fact]
    public void ComputeSides_Coordinator_ShouldGrow()
    {
        // Equal code, but one node depends on all its siblings: it coordinates them
        using var model = NewModel();
        var parent = AddNode(model, model.Root, "Parent");
        var coordinator = AddNode(model, parent, "Coordinator", lines: 20);
        var workers = Enumerable.Range(0, 6).Select(index => AddNode(model, parent, $"W{index}", lines: 20)).ToList();
        workers.ForEach(worker => AddLink(model, coordinator, worker));

        var sides = NodeSizing.ComputeSides([coordinator, .. workers], parent);

        Assert.True(sides[coordinator] > NodeSizing.DefaultSide);
        Assert.All(workers, worker => Assert.Equal(NodeSizing.DefaultSide, sides[worker]));
    }

    [Fact]
    public void ComputeSides_SmallWidelyUsedUtility_ShouldStaySmall()
    {
        // Used by every sibling, but little code: incoming references count only half
        using var model = NewModel();
        var parent = AddNode(model, model.Root, "Parent");
        var utility = AddNode(model, parent, "Utility", lines: 3);
        var users = Enumerable.Range(0, 8).Select(index => AddNode(model, parent, $"U{index}", lines: 20)).ToList();
        users.ForEach(user => AddLink(model, user, utility));

        var sides = NodeSizing.ComputeSides([utility, .. users], parent);

        Assert.True(sides[utility] < NodeSizing.DefaultSide);
    }

    [Fact]
    public void ComputeSides_WidelyUsedUtility_ShouldGrowAtMostOneStep()
    {
        using var model = NewModel();
        var parent = AddNode(model, model.Root, "Parent");
        var utility = AddNode(model, parent, "Utility", lines: 20);
        var users = Enumerable.Range(0, 20).Select(index => AddNode(model, parent, $"U{index}", lines: 20)).ToList();
        users.ForEach(user => AddLink(model, user, utility));

        var sides = NodeSizing.ComputeSides([utility, .. users], parent);

        Assert.True(sides[utility] <= 140);
    }

    [Fact]
    public void CodeMass_Namespace_ShouldSumItsTypes()
    {
        using var model = NewModel();
        var space = AddNode(model, model.Root, "Space", lines: 1);
        AddNode(model, space, "Space.A", lines: 10);
        AddNode(model, space, "Space.B", lines: 30);

        Assert.Equal(40, NodeSizing.CodeMass(space, []));
    }

    [Fact]
    public void CodeMass_NestedTypes_ShouldNotBeCountedTwice()
    {
        // A type's declaration span already covers its nested types and members
        using var model = NewModel();
        var outer = AddNode(model, model.Root, "Outer", lines: 100);
        AddNode(model, outer, "Outer.Inner", lines: 30);
        AddNode(model, outer, "Outer.Method", lines: 1, type: NodeType.MethodMember);

        Assert.Equal(100, NodeSizing.CodeMass(outer, []));
    }

    [Fact]
    public void CodeMass_NodeWithoutSource_ShouldCountItsChildren()
    {
        // External types and members carry no file span; they count as one line each
        using var model = NewModel();
        var external = AddNode(model, model.Root, "External");
        AddNode(model, external, "External.A");
        AddNode(model, external, "External.B");

        Assert.Equal(2, NodeSizing.CodeMass(external, []));
    }

    [Fact]
    public void CodeMass_ManualNode_ShouldBeUnknown()
    {
        using var model = NewModel();
        var manual = AddManualNode(model, model.Root, "Manual");

        Assert.Null(NodeSizing.CodeMass(manual, []));
    }

    static IModel NewModel() => new ModelMgr(new StateMgr()).UseModel();

    static ModelNode AddNode(
        IModel model,
        ModelNode parent,
        string name,
        int? lines = null,
        NodeType type = NodeType.ClassType
    )
    {
        var node = new ModelNode(name, parent) { Type = type };
        if (lines is { } count)
            node.Update(new ParsedNode(name, new() { FileSpan = new FileSpan("File.cs", 10, 10 + count - 1) }));
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static ModelNode AddManualNode(IModel model, ModelNode parent, string name)
    {
        var node = new ModelNode(name, parent) { Type = NodeType.ClassType, IsManual = true };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static void AddLink(IModel model, ModelNode source, ModelNode target)
    {
        var link = new Link(source, target);
        new LineService().AddLinesFromSourceToTarget(model, link);
    }
}
