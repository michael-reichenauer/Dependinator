using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

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
        target.SetUserVisibility(NodeVisibility.Hidden);
        parentA.LineSplitDepth = 1;

        RepLineService.Sync(model, ContainerZoom);

        Assert.True(!GetLine(model, "Source", "ParentB").IsVisible);
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

    [Fact]
    public void Sync_NodeResizedAtSameZoom_ShouldResolveRepsAgain()
    {
        // Whether a node opens can depend on its size (NodeViewPolicy: a node resized far beyond
        // the layout's sizes opens by its size on screen), so a resize (or a layout) at an
        // unchanged zoom can open it: the layout generation makes Sync re-check instead of
        // skipping the unchanged zoom.
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        parentA.LineSplitDepth = 1;
        parentA.Boundary = new Rect(0, 0, 100, 100);

        RepLineService.Sync(model, IconZoom);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);

        new NodeEditCommand(parentA.Id) { Boundary = new Rect(0, 0, 500, 500) }.Execute(model);
        RepLineService.Sync(model, IconZoom);

        Assert.True(GetLine(model, "Source", "ParentB").IsActiveRep);
        Assert.False(GetLine(model, "ParentA", "ParentB").IsActiveRep);
    }

    // A selected stub's links are drawn end to end: from its child, and as deep as the nodes are
    // open on the other side. A stub is one half of what crosses its container's border at the
    // child: the lines into the child, or the lines out of it.
    static LineReveal SelectedStub(Line stub) => LineReveal.None with { Stub = stub };

    [Fact]
    public void Sync_SelectedStub_ShouldLeaveTheOtherChildrensLinksInTheFunnel()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        var other = AddNode(model, "Other", parentA);
        AddLink(model, source, target);
        AddLink(model, other, target);

        RepLineService.Sync(model, ContainerZoom, SelectedStub(GetLine(model, "Source", "ParentA")));

        // One line from the stub's child straight to what it uses in the other container.
        var line = GetLine(model, "Source", "Target");
        Assert.True(line.IsCousin);
        Assert.True(line.IsActiveRep);
        Assert.False(line.IsFocused);
        Assert.Single(line.Links);

        // The other child's link keeps its funnel.
        Assert.True(GetLine(model, "Other", "ParentA").IsActiveRep);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "ParentB", "Target").IsActiveRep);
    }

    [Fact]
    public void Sync_SelectedStub_ShouldStopAtANodeThatIsNotOpen()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var middle = AddNode(model, "Middle", parentB);
        var target = AddNode(model, "Target", middle);
        AddLink(model, source, target);

        // ParentB shows its children, Middle is one of them and still an icon.
        RepLineService.Sync(model, ContainerZoom, SelectedStub(GetLine(model, "Source", "ParentA")));

        Assert.True(GetLine(model, "Source", "Middle").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "Target")));
    }

    [Fact]
    public void Sync_SelectedStubOfAContainer_ShouldDrawTheLinksOfWhatIsInsideIt()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var middle = AddNode(model, "Middle", parentA);
        var source = AddNode(model, "Source", middle);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);

        RepLineService.Sync(model, ContainerZoom, SelectedStub(GetLine(model, "Middle", "ParentA")));

        // The line leaves from the stub's own node, whatever is inside it.
        Assert.True(GetLine(model, "Middle", "Target").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "Target")));
        Assert.False(GetLine(model, "ParentA", "ParentB").IsActiveRep);
    }

    [Fact]
    public void Sync_SelectedStub_ShouldNotDescendIntoRowsTheViewLeavesOut()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        target.Type = Dependinator.Core.Parsing.NodeType.MethodMember;
        AddLink(model, source, target);
        var stub = GetLine(model, "Source", "ParentA");

        // With member lines hidden, a line to the member row would not be drawn at all.
        RepLineService.Sync(
            model,
            ContainerZoom,
            new LineReveal(RowFilter.None, HideMemberLines: true) { Stub = stub }
        );

        Assert.True(GetLine(model, "Source", "ParentB").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "Target")));
    }

    [Fact]
    public void Sync_SelectedLineOfASelection_ShouldStayBetweenItsEnds()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        var other = AddNode(model, "Other", parentB);
        AddLink(model, source, target);
        AddLink(model, source, other);
        RepLineService.Sync(model, ContainerZoom, SelectedStub(GetLine(model, "Source", "ParentA")));
        var clicked = GetLine(model, "Source", "Target");

        // Clicking the line moves the selection from the stub to the line.
        RepLineService.Sync(model, ContainerZoom, LineReveal.None with { KeptLine = clicked });

        Assert.True(clicked.IsActiveRep);
        Assert.Single(clicked.Links);

        // The stub's other line is back in the funnel.
        Assert.False(GetLine(model, "Source", "Other").IsActiveRep);
        Assert.True(GetLine(model, "Source", "ParentA").IsActiveRep);
        Assert.True(GetLine(model, "ParentB", "Other").IsActiveRep);
    }

    [Fact]
    public void Sync_SelectedLineOfASelection_ShouldNotKeepItsInheritanceTwin()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        var member = AddNode(model, "Member", source);
        AddLink(model, member, target);
        AddLink(model, source, target, isInheritance: true);

        // The node inherits the target and a member of it uses the target: a usage stub and an
        // inheritance stub out of the node, each with a line to the target when selected.
        RepLineService.Sync(model, ContainerZoom, SelectedStub(GetLine(model, "Source", "ParentA")));
        var uses = GetLine(model, "Source", "Target");
        Assert.True(uses.IsActiveRep);

        // The usage line clicked: only that line stays between the two nodes. The inheritance,
        // which runs between the same two, stays in the funnel.
        RepLineService.Sync(model, ContainerZoom, LineReveal.None with { KeptLine = uses });

        Assert.True(uses.IsActiveRep);
        Assert.True(uses.IsRevealed);
        Assert.Single(uses.Links);
        Assert.False(model.Lines.ContainsKey(LineId.FromInheritance("Source", "Target")));

        // And the inheritance line, of the other stub, clicked.
        Assert.True(model.Lines.TryGetValue(LineId.FromInheritance("Source", "ParentA"), out var inheritanceStub));
        RepLineService.Sync(model, ContainerZoom, SelectedStub(inheritanceStub!));
        Assert.True(model.Lines.TryGetValue(LineId.FromInheritance("Source", "Target"), out var inherits));
        RepLineService.Sync(model, ContainerZoom, LineReveal.None with { KeptLine = inherits });

        Assert.True(inherits!.IsActiveRep);
        Assert.Single(inherits.Links);
        Assert.False(uses.IsActiveRep);
    }

    [Fact]
    public void Sync_SelectedStub_ShouldGoWithItsLinks_WhenTheExplorerTakesThem()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var user = AddNode(model, "User", parentA);
        var node = AddNode(model, "Node", parentB);
        AddLink(model, user, node);
        var stub = GetLine(model, "ParentB", "Node");

        // The explorer shows what uses the node: its lines replace the stub, selected or not,
        // as they replace any other line.
        model.LineFocus = LineFocus.ForNode(node, isReferences: true);
        model.BumpStructureVersion();
        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub));

        Assert.False(stub.IsActiveRep);
        var focused = model.Lines.Values.Single(line => line.IsFocused && line.IsActiveRep && line.Target == node);
        Assert.False(focused.IsRevealed);
    }

    [Fact]
    public void Sync_SelectionsLines_ShouldNotBeLeftOutByTheLinkCount()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        var other = AddNode(model, "Other", parentB);
        AddLink(model, source, target);
        AddLink(model, source, other);
        var atLeastTwo = LineFilter.None with { MinLinkCount = 2 };

        // At rest the two links share lines, which pass the filter.
        RepLineService.Sync(model, ContainerZoom);
        var bundle = GetLine(model, "ParentA", "ParentB");
        Assert.False(bundle.IsRevealed);
        Assert.False(LineEmphasis.IsLineFilteredOut(bundle, atLeastTwo));

        // With the stub selected they are two lines of one link each, and both are still drawn.
        RepLineService.Sync(model, ContainerZoom, SelectedStub(GetLine(model, "Source", "ParentA")));
        var line = GetLine(model, "Source", "Target");
        Assert.True(line.IsRevealed);
        Assert.False(LineEmphasis.IsLineFilteredOut(line, atLeastTwo));

        // Deselected, a line is judged by its link count again.
        RepLineService.Sync(model, ContainerZoom);
        Assert.False(line.IsRevealed);
        Assert.True(LineEmphasis.IsLineFilteredOut(line, atLeastTwo));
    }

    [Fact]
    public void Sync_SelectedIncomingStub_ShouldDrawOnlyTheLinesIntoItsChild()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var user = AddNode(model, "User", parentA);
        var used = AddNode(model, "Used", parentA);
        var node = AddNode(model, "Node", parentB);
        AddLink(model, user, node); // Into the node from outside its container
        AddLink(model, node, used); // Out of the node
        var stub = GetLine(model, "ParentB", "Node");
        Assert.True(LineStub.IsIncoming(stub));

        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub));

        // What uses the node, end to end, with the stub still there as the selected line.
        var line = GetLine(model, "User", "Node");
        Assert.True(line.IsCousin);
        Assert.True(line.IsActiveRep);
        Assert.True(line.IsRevealed);
        Assert.True(stub.IsActiveRep);
        Assert.False(stub.IsRevealed);

        // What the node uses stays in the funnel: its other stub and the line between the containers.
        Assert.False(model.Lines.ContainsKey(LineId.From("Node", "Used")));
        Assert.True(GetLine(model, "Node", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "ParentB", "ParentA").IsActiveRep);
        Assert.True(GetLine(model, "ParentA", "Used").IsActiveRep);

        // Deselected: the funnel again.
        RepLineService.Sync(model, ContainerZoom);
        Assert.False(line.IsActiveRep);
        Assert.True(stub.IsActiveRep);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
    }

    [Fact]
    public void Sync_SelectedOutgoingStub_ShouldDrawOnlyTheLinesOutOfItsChild()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var user = AddNode(model, "User", parentA);
        var used = AddNode(model, "Used", parentA);
        var node = AddNode(model, "Node", parentB);
        var sibling = AddNode(model, "Sibling", parentB);
        AddLink(model, user, node);
        AddLink(model, node, used);
        AddLink(model, node, sibling); // Stays inside the container: no part of the stub
        var stub = GetLine(model, "Node", "ParentB");
        Assert.True(LineStub.IsOutgoing(stub));

        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub));

        Assert.True(GetLine(model, "Node", "Used").IsActiveRep);
        Assert.True(GetLine(model, "Node", "Used").IsRevealed);
        Assert.True(stub.IsActiveRep);

        // The line to the sibling is as it was, and what uses the node stays in the funnel.
        var toSibling = GetLine(model, "Node", "Sibling");
        Assert.True(toSibling.IsActiveRep);
        Assert.False(toSibling.IsRevealed);
        Assert.False(model.Lines.ContainsKey(LineId.From("User", "Node")));
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "ParentB", "Node").IsActiveRep);
    }

    [Fact]
    public void Sync_SelectedStub_ShouldLeaveTheChildsInheritanceStubAlone()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var user = AddNode(model, "User", parentA);
        var subtype = AddNode(model, "Subtype", parentA);
        var node = AddNode(model, "Node", parentB);
        AddLink(model, user, node);
        AddLink(model, subtype, node, isInheritance: true);

        // Used and inherited from outside: two stubs into the node, each with its own links.
        var stub = GetLine(model, "ParentB", "Node");
        Assert.True(model.Lines.TryGetValue(LineId.FromInheritance("ParentB", "Node"), out var inheritanceStub));
        Assert.True(LineStub.IsStub(inheritanceStub!, isOnPath: false));

        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub));

        Assert.True(GetLine(model, "User", "Node").IsRevealed);
        Assert.False(model.Lines.ContainsKey(LineId.FromInheritance("Subtype", "Node")));
        Assert.True(inheritanceStub!.IsActiveRep);

        // And the other way around.
        RepLineService.Sync(model, ContainerZoom, SelectedStub(inheritanceStub));

        Assert.True(model.Lines.TryGetValue(LineId.FromInheritance("Subtype", "Node"), out var inherits));
        Assert.True(inherits!.IsActiveRep);
        Assert.True(inherits.IsRevealed);
        Assert.False(GetLine(model, "User", "Node").IsActiveRep);
        Assert.True(stub.IsActiveRep);
        Assert.True(inheritanceStub.IsActiveRep);
    }

    [Fact]
    public void GetLineReveal_ShouldAskForAStubsLines_AndKeepACrossingLine_ButNothingForANode()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        var stub = GetLine(model, "ParentB", "Target");
        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub));
        var crossing = GetLine(model, "Source", "Target");
        var between = GetLine(model, "ParentA", "ParentB");

        Assert.Same(stub, Reveal(new RenderSelection(null, stub)).Stub);
        Assert.Same(crossing, Reveal(new RenderSelection(null, crossing)).KeptLine);
        Assert.True(Reveal(new RenderSelection(null, between)).IsEmpty); // An ordinary line

        // A selected node asks for nothing: its stubs are there to click.
        Assert.True(Reveal(new RenderSelection(target, null)).IsEmpty);
        Assert.True(Reveal(new RenderSelection(source, null, [target])).IsEmpty);

        // An edge line drawn in full, on the shown path or for its line points, is selected for
        // itself, not to see where it goes.
        var pathLines = new HashSet<LineId> { stub.Id };
        var selectedStub = new RenderSelection(null, stub);
        Assert.True(NodeTreeSvg.GetLineReveal(selectedStub, RowFilter.None, LineFilter.None, pathLines).IsEmpty);
        stub.IsSegmentsUserSet = true;
        Assert.True(Reveal(new RenderSelection(null, stub)).IsEmpty);

        static LineReveal Reveal(RenderSelection selection) =>
            NodeTreeSvg.GetLineReveal(selection, RowFilter.None, LineFilter.None);
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
