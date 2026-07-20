using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Diagrams;

// Zoom levels used in tests (top-level nodes have GetZoom() == 1, their children 8 with the
// default ContainerZoom of 1/8): at IconZoom (1.0) top-level nodes render as icons; at
// ContainerZoom (0.1) top-level nodes are expanded containers and their children are icons.
//
// Crossing lines go from the deepest visible SOURCE child to the TARGET-side top container;
// the target side never descends (the fan-out inside the target renders via the ordinary
// parent-to-child chain segments, which are not gated by IsActiveRep).
public class RepLineServiceTests
{
    const double IconZoom = 1.0;
    const double ContainerZoom = 0.1;

    [Fact]
    public void Sync_BothSidesCollapsed_ShouldActivateSiblingChainLineOnly()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);

        RepLineService.Sync(model, IconZoom);

        Assert.Equal(3, model.Lines.Count); // No cousin lines added
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.False(GetLine(model, "Source", "ParentA").IsActiveRep);
    }

    [Fact]
    public void Sync_SourceSideExpanded_ShouldCreateChildToContainerLine()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        var link = AddLink(model, source, target);

        RepLineService.Sync(model, ContainerZoom);

        // The crossing line goes from the source child to the target CONTAINER, not to the
        // target child; the split inside ParentB stays with the chain segments.
        var cousin = GetLine(model, "Source", "ParentB");
        Assert.True(cousin.IsCousin);
        Assert.True(cousin.IsActiveRep);
        Assert.Equal(model.Root, cousin.RenderAncestor);
        Assert.Contains(cousin, model.Root.DirectLines);
        Assert.Single(cousin.Links);
        Assert.Contains(cousin, link.Lines);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "Target")));

        Assert.False(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.False(GetLine(model, "Source", "ParentA").IsActiveRep);
    }

    [Fact]
    public void Sync_LinksFromWithinCollapsedNode_ShouldAggregatePerTargetContainer()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var childA = AddNode(model, "ChildA", parentA);
        var childB = AddNode(model, "ChildB", parentB);
        var source1 = AddNode(model, "Source1", childA);
        var source2 = AddNode(model, "Source2", childA);
        var target1 = AddNode(model, "Target1", childB);
        var target2 = AddNode(model, "Target2", childB);
        AddLink(model, source1, target1);
        AddLink(model, source2, target2);

        // ParentA/ParentB expanded, ChildA an icon: both deep links share one crossing line
        // from ChildA to the target container ParentB
        RepLineService.Sync(model, ContainerZoom);

        var cousin = GetLine(model, "ChildA", "ParentB");
        Assert.True(cousin.IsCousin);
        Assert.True(cousin.IsActiveRep);
        Assert.Equal(2, cousin.Links.Count);
    }

    [Fact]
    public void Sync_ExpandedSourceChild_ShouldDescendToDeepestVisibleSource()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        // ParentA renders its children larger, so ChildA is still expanded when top-level
        // containers are; the walk descends through it to Source
        parentA.ContainerZoom = 1.0 / 2;
        var childA = AddNode(model, "ChildA", parentA);
        var childB = AddNode(model, "ChildB", parentB);
        var source = AddNode(model, "Source", childA);
        var target = AddNode(model, "Target", childB);
        AddLink(model, source, target);

        RepLineService.Sync(model, ContainerZoom);

        var cousin = GetLine(model, "Source", "ParentB");
        Assert.True(cousin.IsCousin);
        Assert.True(cousin.IsActiveRep);
        Assert.Equal(model.Root, cousin.RenderAncestor);
    }

    [Fact]
    public void Sync_InheritanceLink_ShouldOnlyBeInheritanceLineWhenSourceRepIsEndpoint()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target, isInheritance: true);

        // Collapsed: reps are the parents, not the endpoints, so the active line is a usage line
        RepLineService.Sync(model, IconZoom);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.FromInheritance("ParentA", "ParentB")));

        // Expanded: the source rep is the real subtype, so the crossing line is inheritance
        // styled at the source end; the target end is the container, not the supertype
        RepLineService.Sync(model, ContainerZoom);
        Assert.True(model.Lines.TryGetValue(LineId.FromInheritance("Source", "ParentB"), out var cousin));
        Assert.True(cousin!.IsInheritance);
        Assert.True(cousin.HasInheritanceSourceEnd);
        Assert.False(cousin.HasInheritanceTargetEnd);
        Assert.True(cousin.IsActiveRep);
    }

    [Fact]
    public void Sync_ZoomOut_ShouldDeactivateCousinLineAndReactivateSiblingLine()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);

        RepLineService.Sync(model, ContainerZoom);
        var cousin = GetLine(model, "Source", "ParentB");

        // The cousin line is kept (invisible) for cheap reactivation, only the flags flip
        RepLineService.Sync(model, IconZoom);
        Assert.True(model.Lines.ContainsKey(cousin.Id));
        Assert.False(cousin.IsActiveRep);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);

        // Zooming back in reuses the kept line instead of creating a new one
        RepLineService.Sync(model, ContainerZoom);
        Assert.Same(cousin, GetLine(model, "Source", "ParentB"));
        Assert.True(cousin.IsActiveRep);
        Assert.False(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.Single(cousin.Links);
    }

    [Fact]
    public void RemoveLink_ShouldRemoveItsCousinLine()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        var link = AddLink(model, source, target);

        RepLineService.Sync(model, ContainerZoom);
        var cousin = GetLine(model, "Source", "ParentB");

        model.RemoveLink(link);

        Assert.False(model.Lines.ContainsKey(cousin.Id));
        Assert.Empty(model.Root.DirectLines);
    }

    [Fact]
    public void Sync_LinkToOwnAncestor_ShouldReuseChainLine()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var childA = AddNode(model, "ChildA", parentA);
        var source = AddNode(model, "Source", childA);
        AddLink(model, source, parentA);

        // ParentA expanded, ChildA icon: reps are (ChildA, ParentA) == the chain segment
        var lineCount = model.Lines.Count;
        RepLineService.Sync(model, ContainerZoom);

        Assert.Equal(lineCount, model.Lines.Count); // No cousin line added
        var chainLine = GetLine(model, "ChildA", "ParentA");
        Assert.False(chainLine.IsCousin);
        Assert.True(chainLine.IsActiveRep);
    }

    [Fact]
    public void Sync_HiddenTarget_ShouldCreateHiddenCousinLine()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        target.SetHidden(true, isUserSet: true);

        RepLineService.Sync(model, ContainerZoom);

        Assert.True(GetLine(model, "Source", "ParentB").IsHidden);
    }

    [Fact]
    public void Sync_PassThroughNode_ShouldNeverBeSourceRepresentative()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var passThrough = AddNode(model, "PassThrough", parentA);
        passThrough.IsPassThrough = true;
        var source = AddNode(model, "Source", passThrough);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);

        // PassThrough would be an icon at this zoom, but pass-through nodes always show their
        // children, so the walk descends to Source
        RepLineService.Sync(model, ContainerZoom);

        Assert.True(GetLine(model, "Source", "ParentB").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("PassThrough", "ParentB")));
    }

    static IModel NewModel() => new ModelMgr(new StateMgr()).UseModel();

    static Node AddNode(IModel model, string name, Node parent)
    {
        var node = new Node(name, parent);
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static Link AddLink(IModel model, Node source, Node target, bool isInheritance = false)
    {
        var link = new Link(source, target) { IsInheritance = isInheritance };
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
        return link;
    }

    static Line GetLine(IModel model, string sourceName, string targetName)
    {
        Assert.True(model.Lines.TryGetValue(LineId.From(sourceName, targetName), out var line));
        return line!;
    }
}
