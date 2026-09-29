using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Diagrams;

// Zoom levels used in tests (top-level nodes have GetZoom() == 1, their children 8 with the
// default ContainerZoom of 1/8): at IconZoom (1.0) top-level nodes render as icons; at
// ContainerZoom (0.1) top-level nodes are expanded containers and their children are icons.
//
// By default every link renders as its aggregated funnel (child-to-parent segments, one
// sibling line between the top siblings, parent-to-child segments). A node's LineSplitDepth
// lets the representative walk descend that many levels into it, on either side, as far as
// the nodes show their children; deeper rep pairs become crossing ("cousin") lines.
public class RepLineServiceTests
{
    const double IconZoom = 1.0;
    const double ContainerZoom = 0.1;

    [Fact]
    public void Sync_Default_ShouldActivateWholeFunnelChain()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);

        // Expanded containers, but no split depth anywhere: the funnel is drawn as-is
        RepLineService.Sync(model, ContainerZoom);

        Assert.Equal(3, model.Lines.Count); // No cousin lines added
        Assert.True(GetLine(model, "Source", "ParentA").IsActiveRep);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "ParentB", "Target").IsActiveRep);
        Assert.Empty(model.Root.DirectLines);
    }

    [Fact]
    public void Sync_DepthOnSource_ShouldCreateChildToContainerLine()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        var link = AddLink(model, source, target);
        parentA.LineSplitDepth = 1;

        RepLineService.Sync(model, ContainerZoom);

        // The crossing line goes from the source child to the target CONTAINER; the fan-out
        // inside ParentB stays with the parent-to-child chain segment.
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
        Assert.True(GetLine(model, "ParentB", "Target").IsActiveRep);
    }

    [Fact]
    public void Sync_DepthOnSource_ShouldNotDescend_WhenChildrenAreNotShown()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        parentA.LineSplitDepth = 1;

        // ParentA is an icon at this zoom: the visible level caps the depth
        RepLineService.Sync(model, IconZoom);

        Assert.Equal(3, model.Lines.Count);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
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
        parentA.LineSplitDepth = 2;

        // ParentA expanded but ChildA an icon: the walk stops at ChildA although the depth
        // allows one more level, so both deep links share one crossing line to ParentB
        RepLineService.Sync(model, ContainerZoom);

        var cousin = GetLine(model, "ChildA", "ParentB");
        Assert.True(cousin.IsCousin);
        Assert.True(cousin.IsActiveRep);
        Assert.Equal(2, cousin.Links.Count);
    }

    [Fact]
    public void Sync_DepthTwo_ShouldDescendToDeepestVisibleSource()
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
        parentA.LineSplitDepth = 2;

        RepLineService.Sync(model, ContainerZoom);

        var cousin = GetLine(model, "Source", "ParentB");
        Assert.True(cousin.IsCousin);
        Assert.True(cousin.IsActiveRep);
        Assert.Equal(model.Root, cousin.RenderAncestor);
    }

    [Fact]
    public void Sync_DepthOne_ShouldStopAtFirstLevel_WhenChildIsExpanded()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        parentA.ContainerZoom = 1.0 / 2;
        var childA = AddNode(model, "ChildA", parentA);
        var source = AddNode(model, "Source", childA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        parentA.LineSplitDepth = 1;

        // ChildA is expanded, but the depth is spent: the line ends at ChildA's border and the
        // funnel continues inside it
        RepLineService.Sync(model, ContainerZoom);

        Assert.True(GetLine(model, "ChildA", "ParentB").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "ParentB")));
        Assert.True(GetLine(model, "Source", "ChildA").IsActiveRep);
    }

    [Fact]
    public void Sync_ChildDepth_ShouldExtendOnlyItsBranch()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        parentA.ContainerZoom = 1.0 / 2;
        var childA1 = AddNode(model, "ChildA1", parentA);
        var childA2 = AddNode(model, "ChildA2", parentA);
        var source1 = AddNode(model, "Source1", childA1);
        var source2 = AddNode(model, "Source2", childA2);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source1, target);
        AddLink(model, source2, target);
        parentA.LineSplitDepth = 1;
        childA1.LineSplitDepth = 1;

        RepLineService.Sync(model, ContainerZoom);

        Assert.True(GetLine(model, "Source1", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "ChildA2", "ParentB").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source2", "ParentB")));
    }

    [Fact]
    public void Sync_DepthOnTarget_ShouldDescendIntoTarget()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        parentB.LineSplitDepth = 1;

        RepLineService.Sync(model, ContainerZoom);

        var cousin = GetLine(model, "ParentA", "Target");
        Assert.True(cousin.IsCousin);
        Assert.True(cousin.IsActiveRep);
        Assert.Equal(model.Root, cousin.RenderAncestor);
        Assert.True(GetLine(model, "Source", "ParentA").IsActiveRep);
        Assert.False(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.False(GetLine(model, "ParentB", "Target").IsActiveRep);
    }

    [Fact]
    public void Sync_DepthOnBothEnds_ShouldConnectChildren()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        parentA.LineSplitDepth = 1;
        parentB.LineSplitDepth = 1;

        RepLineService.Sync(model, ContainerZoom);

        var cousin = GetLine(model, "Source", "Target");
        Assert.True(cousin.IsCousin);
        Assert.True(cousin.IsActiveRep);
        Assert.False(GetLine(model, "Source", "ParentA").IsActiveRep);
        Assert.False(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.False(GetLine(model, "ParentB", "Target").IsActiveRep);
    }

    [Fact]
    public void Sync_DepthOnAncestor_ShouldSplitInternalLinks()
    {
        using var model = NewModel();
        var container = AddNode(model, "Container", model.Root);
        // Container renders its children larger, so X and Y are expanded when Container is
        container.ContainerZoom = 1.0 / 2;
        var x = AddNode(model, "X", container);
        var y = AddNode(model, "Y", container);
        var source = AddNode(model, "Source", x);
        var target = AddNode(model, "Target", y);
        AddLink(model, source, target);

        // Depth 1: the link's top siblings X and Y are the container's children already, so
        // the sibling line is the split; nothing crosses
        container.LineSplitDepth = 1;
        RepLineService.Sync(model, ContainerZoom);
        Assert.True(GetLine(model, "X", "Y").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "Target")));

        // Depth 2 counted from the container reaches one level below X and Y
        container.LineSplitDepth = 2;
        model.BumpStructureVersion();
        RepLineService.Sync(model, ContainerZoom);
        var cousin = GetLine(model, "Source", "Target");
        Assert.True(cousin.IsActiveRep);
        Assert.Equal(container, cousin.RenderAncestor);
        Assert.False(GetLine(model, "X", "Y").IsActiveRep);
    }

    [Fact]
    public void Sync_PassThroughNode_ShouldNotConsumeLevel()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var passThrough = AddNode(model, "PassThrough", parentA);
        passThrough.IsPassThrough = true;
        var source = AddNode(model, "Source", passThrough);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        parentA.LineSplitDepth = 1;

        // PassThrough is invisible chrome, so one level below ParentA is Source, not
        // PassThrough
        RepLineService.Sync(model, ContainerZoom);

        Assert.True(GetLine(model, "Source", "ParentB").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("PassThrough", "ParentB")));
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

        // Aggregated: reps are the parents, not the endpoints, so the active line is a usage line
        RepLineService.Sync(model, ContainerZoom);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.FromInheritance("ParentA", "ParentB")));

        // Split: the source rep is the real subtype, so the crossing line is inheritance
        // styled at the source end; the target end is the container, not the supertype
        parentA.LineSplitDepth = 1;
        model.BumpStructureVersion();
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
        parentA.LineSplitDepth = 1;

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
    public void Sync_DepthReset_ShouldRestoreFunnel()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        parentA.LineSplitDepth = 1;
        RepLineService.Sync(model, ContainerZoom);
        var cousin = GetLine(model, "Source", "ParentB");
        var lineCount = model.Lines.Count;

        parentA.LineSplitDepth = 0;
        model.BumpStructureVersion();
        RepLineService.Sync(model, ContainerZoom);

        Assert.Equal(lineCount, model.Lines.Count); // The cousin line is kept, just inactive
        Assert.False(cousin.IsActiveRep);
        Assert.True(GetLine(model, "Source", "ParentA").IsActiveRep);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "ParentB", "Target").IsActiveRep);
    }

    [Fact]
    public void Sync_DepthChange_ShouldRequireStructureVersionBump()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        RepLineService.Sync(model, ContainerZoom);

        // Without a bump the memo hits (no node was visited, so no expansion state changed)
        parentA.LineSplitDepth = 1;
        RepLineService.Sync(model, ContainerZoom);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "ParentB")));

        model.BumpStructureVersion();
        RepLineService.Sync(model, ContainerZoom);
        Assert.True(GetLine(model, "Source", "ParentB").IsActiveRep);
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
        parentA.LineSplitDepth = 1;

        RepLineService.Sync(model, ContainerZoom);
        var cousin = GetLine(model, "Source", "ParentB");

        model.RemoveLink(link);

        Assert.False(model.Lines.ContainsKey(cousin.Id));
        Assert.Empty(model.Root.DirectLines);
    }

    [Fact]
    public void Sync_LinkToOwnAncestor_ShouldActivateChainSegments()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var childA = AddNode(model, "ChildA", parentA);
        var source = AddNode(model, "Source", childA);
        AddLink(model, source, parentA);
        var lineCount = model.Lines.Count;

        // Aggregated: both reps are ParentA itself, the link is drawn by its funnel alone
        RepLineService.Sync(model, ContainerZoom);
        Assert.Equal(lineCount, model.Lines.Count); // No cousin line added
        Assert.True(GetLine(model, "Source", "ChildA").IsActiveRep);
        Assert.True(GetLine(model, "ChildA", "ParentA").IsActiveRep);

        // Split one level: reps are (ChildA, ParentA) == the chain segment, which is reused
        parentA.LineSplitDepth = 1;
        model.BumpStructureVersion();
        RepLineService.Sync(model, ContainerZoom);
        Assert.Equal(lineCount, model.Lines.Count);
        var chainLine = GetLine(model, "ChildA", "ParentA");
        Assert.False(chainLine.IsCousin);
        Assert.True(chainLine.IsActiveRep);
        Assert.True(GetLine(model, "Source", "ChildA").IsActiveRep);
    }

    [Fact]
    public void Sync_ParentToOwnChildLink_ShouldActivateParentToChildSegments()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var childA = AddNode(model, "ChildA", parentA);
        var target = AddNode(model, "Target", childA);
        AddLink(model, parentA, target);

        RepLineService.Sync(model, ContainerZoom);

        Assert.Equal(2, model.Lines.Count);
        Assert.True(GetLine(model, "ParentA", "ChildA").IsActiveRep);
        Assert.True(GetLine(model, "ChildA", "Target").IsActiveRep);
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
        parentA.LineSplitDepth = 1;

        RepLineService.Sync(model, ContainerZoom);

        Assert.True(GetLine(model, "Source", "ParentB").IsHidden);
    }

    // --- Explorer focus (Model.LineFocus): the subject's links leave from the subject itself and
    // split into the far containers expanded in the explorer tree.

    [Fact]
    public void Sync_FocusNode_ShouldPinSubjectAndAggregateFar()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        model.LineFocus = LineFocus.ForNode(source, isReferences: false);

        RepLineService.Sync(model, ContainerZoom);

        // The line leaves from Source (not from ParentA's bundle) and ends at ParentB's border,
        // like the collapsed top row of the explorer tree; the fan-out inside stays ordinary
        var cousin = GetLine(model, "Source", "ParentB");
        Assert.True(cousin.IsActiveRep);
        Assert.True(cousin.IsFocused);
        Assert.False(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "ParentB", "Target").IsActiveRep);
        Assert.False(GetLine(model, "ParentB", "Target").IsFocused);
    }

    [Fact]
    public void Sync_FocusNode_ShouldDescendIntoExpandedFarNode()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        var focus = LineFocus.ForNode(source, isReferences: false);
        focus.ExpandedFarNodes.Add(parentB);
        model.LineFocus = focus;

        RepLineService.Sync(model, ContainerZoom);

        var cousin = GetLine(model, "Source", "Target");
        Assert.True(cousin.IsActiveRep);
        Assert.True(cousin.IsFocused);
        Assert.False(GetLine(model, "ParentB", "Target").IsActiveRep);
    }

    [Fact]
    public void Sync_FocusNode_ShouldStopAtUnexpandedChildOfExpandedNode()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        parentB.ContainerZoom = 1.0 / 2; // ChildB is an expanded container at ContainerZoom
        var source = AddNode(model, "Source", parentA);
        var childB = AddNode(model, "ChildB", parentB);
        var target = AddNode(model, "Target", childB);
        AddLink(model, source, target);
        var focus = LineFocus.ForNode(source, isReferences: false);
        focus.ExpandedFarNodes.Add(parentB);
        model.LineFocus = focus;

        // Only the ParentB row is expanded: one level, to ChildB's border
        RepLineService.Sync(model, ContainerZoom);
        Assert.True(GetLine(model, "Source", "ChildB").IsFocused);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "Target")));
        Assert.True(GetLine(model, "ChildB", "Target").IsActiveRep);

        // Expanding the ChildB row too reaches Target
        focus.ExpandedFarNodes.Add(childB);
        model.BumpStructureVersion();
        RepLineService.Sync(model, ContainerZoom);
        Assert.True(GetLine(model, "Source", "Target").IsFocused);
        Assert.False(GetLine(model, "ChildB", "Target").IsActiveRep);
    }

    [Fact]
    public void Sync_FocusLine_ShouldOnlyAffectItsLinks()
    {
        using var model = NewModel();
        var parentA1 = AddNode(model, "ParentA1", model.Root);
        var parentA2 = AddNode(model, "ParentA2", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, parentA1, target);
        AddLink(model, parentA2, target);
        var focus = LineFocus.ForLine(GetLine(model, "ParentA1", "ParentB"), isReferences: false);
        focus.ExpandedFarNodes.Add(parentB);
        model.LineFocus = focus;

        RepLineService.Sync(model, ContainerZoom);

        // The subject line's link splits into ParentB; the other line into ParentB does not
        Assert.True(GetLine(model, "ParentA1", "Target").IsFocused);
        Assert.False(GetLine(model, "ParentA1", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "ParentA2", "ParentB").IsActiveRep);
        Assert.False(GetLine(model, "ParentA2", "ParentB").IsFocused);
        Assert.True(GetLine(model, "ParentB", "Target").IsActiveRep); // Still needed by ParentA2's link
    }

    [Fact]
    public void Sync_FocusReferences_ShouldPinTargetAndDescendSourceSide()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        var focus = LineFocus.ForNode(target, isReferences: true);
        model.LineFocus = focus;

        // Collapsed tree: the referencing container's bundle ends at Target itself
        RepLineService.Sync(model, ContainerZoom);
        Assert.True(GetLine(model, "ParentA", "Target").IsFocused);
        Assert.True(GetLine(model, "Source", "ParentA").IsActiveRep);

        // Expanding the ParentA row reveals the referencing node
        focus.ExpandedFarNodes.Add(parentA);
        model.BumpStructureVersion();
        RepLineService.Sync(model, ContainerZoom);
        Assert.True(GetLine(model, "Source", "Target").IsFocused);
        Assert.False(GetLine(model, "Source", "ParentA").IsActiveRep);
    }

    [Fact]
    public void Sync_FocusOnFanOutSegmentLine_ShouldDescendToItsFarEnd()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        // The explorer opened from the ParentB->Target fan-out segment: its near end is not on
        // the link's source path (no pin there), its far end pins the target side at Target
        model.LineFocus = LineFocus.ForLine(GetLine(model, "ParentB", "Target"), isReferences: false);

        RepLineService.Sync(model, ContainerZoom);

        Assert.True(GetLine(model, "ParentA", "Target").IsFocused);
        Assert.True(GetLine(model, "Source", "ParentA").IsActiveRep);
        Assert.False(GetLine(model, "ParentB", "Target").IsActiveRep);
    }

    [Fact]
    public void Sync_FocusCleared_ShouldRestoreFunnel()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        model.LineFocus = LineFocus.ForNode(source, isReferences: false);
        RepLineService.Sync(model, ContainerZoom);
        var cousin = GetLine(model, "Source", "ParentB");

        model.LineFocus = null;
        model.BumpStructureVersion();
        RepLineService.Sync(model, ContainerZoom);

        Assert.False(cousin.IsActiveRep);
        Assert.False(cousin.IsFocused);
        Assert.True(GetLine(model, "Source", "ParentA").IsActiveRep);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.False(GetLine(model, "ParentA", "ParentB").IsFocused);
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
