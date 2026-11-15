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
// sibling line between the top siblings, parent-to-child segments). The explorer's focus and a
// selected stub let the representative walk descend into the containers, as far as the nodes
// show their children; deeper rep pairs become crossing ("cousin") lines.
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
    public void Sync_PassThroughNode_ShouldNotStopTheWalk()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var passThrough = AddNode(model, "PassThrough", parentA);
        passThrough.IsPassThrough = true;
        var source = AddNode(model, "Source", passThrough);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        var focus = LineFocus.ForNode(target, isReferences: true);
        focus.ExpandedFarNodes.Add(parentA);
        model.LineFocus = focus;

        // PassThrough is invisible chrome, so the row below the expanded ParentA is Source, not
        // PassThrough
        RepLineService.Sync(model, ContainerZoom);

        Assert.True(GetLine(model, "Source", "Target").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("PassThrough", "Target")));
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

        // Focused: the source rep is the real subtype, so the crossing line is inheritance
        // styled at the source end; the target end is the container, not the supertype
        model.LineFocus = LineFocus.ForNode(source, isReferences: false);
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
        model.LineFocus = LineFocus.ForNode(source, isReferences: false);

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
        model.LineFocus = LineFocus.ForNode(source, isReferences: false);

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

        // Both reps are ParentA itself, the link is drawn by its funnel alone
        RepLineService.Sync(model, ContainerZoom);
        Assert.Equal(lineCount, model.Lines.Count); // No cousin line added
        Assert.True(GetLine(model, "Source", "ChildA").IsActiveRep);
        Assert.True(GetLine(model, "ChildA", "ParentA").IsActiveRep);
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
        model.LineFocus = LineFocus.ForNode(source, isReferences: false);

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
    public void Sync_FocusNode_ShouldNotDescend_WhenChildrenAreNotShown()
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

        // Both parents are icons at this zoom: the visible level caps the descent on both sides
        RepLineService.Sync(model, IconZoom);

        Assert.Equal(3, model.Lines.Count);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
    }

    [Fact]
    public void Sync_FocusOnInternalLink_ShouldDrawTheCrossingLineInsideTheContainer()
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
        var focus = LineFocus.ForNode(source, isReferences: false);
        focus.ExpandedFarNodes.Add(y);
        model.LineFocus = focus;

        RepLineService.Sync(model, ContainerZoom);

        var cousin = GetLine(model, "Source", "Target");
        Assert.True(cousin.IsActiveRep);
        Assert.Equal(container, cousin.RenderAncestor);
        Assert.False(GetLine(model, "X", "Y").IsActiveRep);
    }

    [Fact]
    public void Sync_FocusRepsEqualToAChainSegment_ShouldReuseIt()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var childA = AddNode(model, "ChildA", parentA);
        var source = AddNode(model, "Source", childA);
        AddLink(model, source, parentA);
        var lineCount = model.Lines.Count;

        // Pinned at ChildA: the reps are (ChildA, ParentA), the chain segment, which is reused
        model.LineFocus = LineFocus.ForNode(childA, isReferences: false);
        RepLineService.Sync(model, ContainerZoom);

        Assert.Equal(lineCount, model.Lines.Count);
        var chainLine = GetLine(model, "ChildA", "ParentA");
        Assert.False(chainLine.IsCousin);
        Assert.True(chainLine.IsActiveRep);
        Assert.True(GetLine(model, "Source", "ChildA").IsActiveRep);
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
        model.LineFocus = LineFocus.ForNode(source, isReferences: false);
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
    public void Sync_SelectedNode_ShouldLeaveTheFunnelAsItIs()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        source.IsSelected = true;

        // What a selected node asks of the lines: nothing. Its stubs are there to click.
        var reveal = NodeTreeSvg.GetLineReveal(LineEmphasis.FindSelection(model), RowFilter.None, LineFilter.None);
        RepLineService.Sync(model, ContainerZoom, reveal);

        Assert.DoesNotContain(model.Lines.Values, line => line.IsCousin || line.IsRevealed);
        Assert.True(GetLine(model, "Source", "ParentA").IsActiveRep);
        Assert.True(GetLine(model, "ParentA", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "ParentB", "Target").IsActiveRep);
    }

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
    public void Sync_ClickedLineOfAStub_ShouldStayBetweenItsEnds()
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
    public void Sync_ClickedLineOfAnExploredStub_ShouldKeepTheStubsOtherLines()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        var other = AddNode(model, "Other", parentB);
        AddLink(model, source, target);
        AddLink(model, source, other);
        var stub = GetLine(model, "Source", "ParentA");
        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub));
        var clicked = GetLine(model, "Source", "Target");
        clicked.IsSelected = true;
        stub.IsExplored = true;

        // The selection asks for the clicked line and for the stub it belongs to.
        var reveal = NodeTreeSvg.GetLineReveal(LineEmphasis.FindSelection(model), RowFilter.None, LineFilter.None);
        Assert.Equal(clicked, reveal.KeptLine);
        Assert.Equal(stub, reveal.Stub);
        RepLineService.Sync(model, ContainerZoom, reveal);

        // Both lines stay, lit beside the selected one, and the stub with them.
        Assert.True(clicked.IsActiveRep);
        var otherLine = GetLine(model, "Source", "Other");
        Assert.True(otherLine.IsActiveRep);
        Assert.True(otherLine.IsRevealed);
        Assert.True(stub.IsActiveRep);
        Assert.False(LineEmphasis.IsLineDimmed(otherLine, LineEmphasis.FindSelection(model)));
        Assert.False(LineEmphasis.IsLineDimmed(stub, LineEmphasis.FindSelection(model)));
    }

    [Fact]
    public void Sync_ClickedLineOfAnExploredStub_ShouldStayWhole_WhileTheOthersSplit()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        parentA.Boundary = new Rect(0, 0, 100, 100);
        parentB.Boundary = new Rect(1000, 0, 100, 100);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        var other = AddNode(model, "Other", parentB);
        var middle = AddNode(model, "Middle", model.Root);
        middle.Boundary = new Rect(1000, 500, 100, 100);
        var third = AddNode(model, "Third", middle);
        AddLink(model, source, target);
        AddLink(model, source, other);
        AddLink(model, source, third);
        var stub = GetLine(model, "Source", "ParentA");
        var farAway = new Rect(-50, -50, 300, 300);

        // ParentB is out of the tile: one line to it, clicked.
        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub) with { Area = farAway });
        var clicked = GetLine(model, "Source", "ParentB");
        Assert.True(clicked.IsActiveRep);

        // Panned to where both containers are: the clicked line keeps its two links, the stub's
        // other far end splits.
        var both = new Rect(-50, -50, 1200, 700);
        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub) with { KeptLine = clicked, Area = both });

        Assert.True(clicked.IsActiveRep);
        Assert.Equal(2, clicked.Links.Count);
        Assert.True(GetLine(model, "Source", "Third").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "Target")));

        // The memo walks the stub's far ends again on the next tile, leaving the kept links out.
        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub) with { KeptLine = clicked, Area = farAway });
        Assert.True(clicked.IsActiveRep);
        Assert.False(GetLine(model, "Source", "Third").IsActiveRep);
        Assert.True(GetLine(model, "Source", "Middle").IsActiveRep);
    }

    [Fact]
    public void Sync_ClickedLineOfAStub_ShouldNotKeepItsInheritanceTwin()
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

    // A busy stub's lines end at a far container that is out of the tile, or whose children
    // would be tiny on screen: one line per container instead of one per far end.
    [Fact]
    public void Sync_SelectedStub_ShouldEndAtAFarContainerOutsideTheArea_UntilItIsInIt()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        parentA.Boundary = new Rect(0, 0, 100, 100);
        parentB.Boundary = new Rect(1000, 0, 100, 100);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        var other = AddNode(model, "Other", parentB);
        AddLink(model, source, target);
        AddLink(model, source, other);
        var stub = GetLine(model, "Source", "ParentA");

        // The tile shows ParentA only: one line to ParentB, with both links.
        var aOnly = SelectedStub(stub) with { Area = new Rect(-50, -50, 300, 300) };
        RepLineService.Sync(model, ContainerZoom, aOnly);

        var line = GetLine(model, "Source", "ParentB");
        Assert.True(line.IsActiveRep);
        Assert.True(line.IsRevealed);
        Assert.Equal(2, line.Links.Count);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "Target")));
        Assert.True(stub.IsActiveRep);

        // A tile that reaches ParentB (panned there) splits the line, at the same zoom.
        var both = SelectedStub(stub) with { Area = new Rect(-50, -50, 1100, 300) };
        RepLineService.Sync(model, ContainerZoom, both);

        Assert.False(line.IsActiveRep);
        Assert.True(GetLine(model, "Source", "Target").IsActiveRep);
        Assert.True(GetLine(model, "Source", "Other").IsActiveRep);
    }

    [Fact]
    public void Sync_SelectedStub_ShouldKeepItsLines_OnATileWhereNoFarEndChanges()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        parentA.Boundary = new Rect(0, 0, 100, 100);
        parentB.Boundary = new Rect(1000, 0, 100, 100);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        var stub = GetLine(model, "Source", "ParentA");
        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub) with { Area = new Rect(-50, -50, 300, 300) });

        // The next tile still leaves ParentB out: the same lines (the memo walks only the stub's
        // links to see that).
        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub) with { Area = new Rect(250, -50, 300, 300) });

        Assert.True(GetLine(model, "Source", "ParentB").IsActiveRep);
        Assert.True(stub.IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "Target")));

        // And the tile after it reaches ParentB.
        RepLineService.Sync(model, ContainerZoom, SelectedStub(stub) with { Area = new Rect(550, -50, 600, 300) });

        Assert.False(GetLine(model, "Source", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "Source", "Target").IsActiveRep);
    }

    [Fact]
    public void Sync_SelectedStub_ShouldSplitTheLineOfAFarContainerALayoutMovesIntoTheTile()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        parentA.Boundary = new Rect(0, 0, 100, 100);
        parentB.Boundary = new Rect(1000, 0, 100, 100);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        var reveal = SelectedStub(GetLine(model, "Source", "ParentA")) with { Area = new Rect(-50, -50, 300, 300) };
        RepLineService.Sync(model, ContainerZoom, reveal);
        Assert.True(GetLine(model, "Source", "ParentB").IsActiveRep);

        // The same tile and zoom, but ParentB moved into it.
        parentB.Boundary = new Rect(120, 0, 100, 100);
        NodeLayout.BumpGeneration();
        RepLineService.Sync(model, ContainerZoom, reveal);

        Assert.False(GetLine(model, "Source", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "Source", "Target").IsActiveRep);
    }

    [Fact]
    public void Sync_SelectedStub_ShouldEndAtAFarContainerWithTinyChildren_UntilZoomedIn()
    {
        using var model = NewModel();
        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
        var reveal = SelectedStub(GetLine(model, "Source", "ParentA"));

        // ParentB is open, but a child of the default size would be about 12 pixels.
        parentB.ContainerZoom = 1.0 / 80;
        Assert.True(NodeViewPolicy.IsChildrenShown(parentB, ContainerZoom));
        RepLineService.Sync(model, ContainerZoom, reveal);

        Assert.True(GetLine(model, "Source", "ParentB").IsActiveRep);
        Assert.False(model.Lines.ContainsKey(LineId.From("Source", "Target")));

        // Zoomed in, the same selection draws the line to the child: no node opened or closed,
        // so this must not be skipped as an unchanged sync.
        RepLineService.Sync(model, ContainerZoom / 2, reveal);

        Assert.False(GetLine(model, "Source", "ParentB").IsActiveRep);
        Assert.True(GetLine(model, "Source", "Target").IsActiveRep);
    }

    [Fact]
    public void GetCanvasArea_ShouldBeTheTileWithItsMargin_InCanvasUnits()
    {
        var area = SvgService.GetCanvasArea(new Rect(1000, 500, 100, 50), new Rect(-60, -30, 220, 110), 2);

        Assert.Equal(new Rect(1880, 940, 440, 220), area);
    }

    [Fact]
    public void Sync_SelectedStubsLines_ShouldNotBeLeftOutByTheLinkCount()
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
        Assert.True(stub.IsRevealed); // Lit with its lines, also while one of them is selected

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
