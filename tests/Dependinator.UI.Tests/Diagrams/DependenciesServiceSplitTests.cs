using Dependinator.UI.Diagrams.Dependencies;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Diagrams;

// Splitting an aggregated line into its target down to the deepest currently-visible level:
// dashed direct-style lines from the same source to each visible representative descendant of
// the target temporarily replace the line (see DependenciesService.SplitLine). Uses chain
// lines built by LineService; the same behavior applies to cousin crossing lines (both are
// aggregated lines with links).
//
// The split reads the model's rendered zoom. Top-level nodes have GetZoom() == 1 and their
// children 8 (default ContainerZoom 1/8): at OneLevelZoom the target's children are icons (so
// the split stops at them); at DeepZoom the target's children are expanded containers (so the
// split descends through them to their icon children).
public class DependenciesServiceSplitTests
{
    const double OneLevelZoom = 0.1;
    const double DeepZoom = 0.05;

    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly Mock<ISelectionService> selectionService = new();
    readonly Mock<IApplicationEvents> applicationEvents = new();
    readonly Mock<INavigationService> navigationService = new();

    DependenciesService CreateService()
    {
        selectionService.Setup(s => s.SelectedId).Returns(PointerId.Empty);
        return new(selectionService.Object, applicationEvents.Object, modelMgr, navigationService.Object);
    }

    [Fact]
    public void SplitLine_ShouldCreateDirectStyleLinePerVisibleTargetChild()
    {
        var service = CreateService();
        using (var model = modelMgr.UseModel())
        {
            var parentA = AddNode(model, "ParentA", model.Root);
            var parentB = AddNode(model, "ParentB", model.Root);
            var source = AddNode(model, "Source", parentA);
            var childB1 = AddNode(model, "ChildB1", parentB);
            var childB2 = AddNode(model, "ChildB2", parentB);
            var target1 = AddNode(model, "Target1", childB1);
            AddLink(model, source, target1);
            AddLink(model, source, childB2);
            model.Zoom = OneLevelZoom; // ParentB's children are icons
        }

        service.SplitLine(LineId.From("ParentA", "ParentB"));

        using (var model = modelMgr.UseModel())
        {
            var original = model.Lines[LineId.From("ParentA", "ParentB")];

            // One dashed line per distinct visible child of ParentB the links continue into
            // (both are icons at this zoom, so the split stops at them).
            var split1 = model.Lines[LineId.FromDirect("ParentA", "ChildB1")];
            var split2 = model.Lines[LineId.FromDirect("ParentA", "ChildB2")];
            Assert.True(split1.IsDirect);
            Assert.Same(original, split1.SplitParent);
            Assert.Same(original, split2.SplitParent);
            Assert.Single(split1.Links);
            Assert.Single(split2.Links);
            Assert.Equal([split1, split2], original.SplitLines.OrderBy(l => l.Target.Name).ToList());

            // All links are represented by split lines, so the original hides
            Assert.True(original.IsSplitSuppressed);
        }
    }

    [Fact]
    public void SplitLine_ShouldDescendToDeepestVisible_WhenIntermediateIsExpanded()
    {
        var service = CreateService();
        using (var model = modelMgr.UseModel())
        {
            var parentA = AddNode(model, "ParentA", model.Root);
            var parentB = AddNode(model, "ParentB", model.Root);
            var source = AddNode(model, "Source", parentA);
            var childB1 = AddNode(model, "ChildB1", parentB);
            var target1 = AddNode(model, "Target1", childB1);
            AddLink(model, source, target1);
            model.Zoom = DeepZoom; // ChildB1 is an expanded container, Target1 an icon
        }

        service.SplitLine(LineId.From("ParentA", "ParentB"));

        using (var model = modelMgr.UseModel())
        {
            // The split reaches Target1 directly, skipping the expanded ChildB1 (which already
            // shows its own structure).
            Assert.True(model.Lines.ContainsKey(LineId.FromDirect("ParentA", "Target1")));
            Assert.False(model.Lines.ContainsKey(LineId.FromDirect("ParentA", "ChildB1")));
            Assert.True(model.Lines[LineId.From("ParentA", "ParentB")].IsSplitSuppressed);
        }
    }

    [Fact]
    public void SplitLineSource_ShouldCreateDirectStyleLinePerVisibleSourceChild()
    {
        var service = CreateService();
        using (var model = modelMgr.UseModel())
        {
            var parentA = AddNode(model, "ParentA", model.Root);
            var parentB = AddNode(model, "ParentB", model.Root);
            var childA1 = AddNode(model, "ChildA1", parentA);
            var childA2 = AddNode(model, "ChildA2", parentA);
            var source1 = AddNode(model, "Source1", childA1);
            var target = AddNode(model, "Target", parentB);
            AddLink(model, source1, target);
            AddLink(model, childA2, target);
            model.Zoom = OneLevelZoom; // ParentA's children are icons
        }

        service.SplitLineSource(LineId.From("ParentA", "ParentB"));

        using (var model = modelMgr.UseModel())
        {
            var original = model.Lines[LineId.From("ParentA", "ParentB")];

            // One dashed line per distinct visible source child of ParentA, target fixed
            var split1 = model.Lines[LineId.FromDirect("ChildA1", "ParentB")];
            var split2 = model.Lines[LineId.FromDirect("ChildA2", "ParentB")];
            Assert.True(split1.IsDirect);
            Assert.Same(original, split1.SplitParent);
            Assert.Same(original, split2.SplitParent);
            Assert.Single(split1.Links);
            Assert.Single(split2.Links);
            Assert.True(original.IsSplitSuppressed);
        }
    }

    [Fact]
    public void SplitLineSource_ShouldDescendToDeepestVisible_WhenIntermediateIsExpanded()
    {
        var service = CreateService();
        using (var model = modelMgr.UseModel())
        {
            var parentA = AddNode(model, "ParentA", model.Root);
            var parentB = AddNode(model, "ParentB", model.Root);
            var childA1 = AddNode(model, "ChildA1", parentA);
            var source1 = AddNode(model, "Source1", childA1);
            var target = AddNode(model, "Target", parentB);
            AddLink(model, source1, target);
            model.Zoom = DeepZoom; // ChildA1 is an expanded container, Source1 an icon
        }

        service.SplitLineSource(LineId.From("ParentA", "ParentB"));

        using (var model = modelMgr.UseModel())
        {
            // The split reaches Source1 directly, skipping the expanded ChildA1.
            Assert.True(model.Lines.ContainsKey(LineId.FromDirect("Source1", "ParentB")));
            Assert.False(model.Lines.ContainsKey(LineId.FromDirect("ChildA1", "ParentB")));
            Assert.True(model.Lines[LineId.From("ParentA", "ParentB")].IsSplitSuppressed);
        }
    }

    [Fact]
    public void CanSplitLineSource_ShouldBeFalse_WhenAllLinksStartAtSource()
    {
        var service = CreateService();
        using (var model = modelMgr.UseModel())
        {
            var parentA = AddNode(model, "ParentA", model.Root);
            var parentB = AddNode(model, "ParentB", model.Root);
            var target = AddNode(model, "Target", parentB);
            AddLink(model, parentA, target); // Starts at the source container itself
            model.Zoom = OneLevelZoom;
        }

        Assert.False(service.CanSplitLineSource(LineId.From("ParentA", "ParentB")));
    }

    [Fact]
    public void SplitLine_ShouldKeepOriginalVisible_WhenSomeLinksEndAtTarget()
    {
        var service = CreateService();
        using (var model = modelMgr.UseModel())
        {
            var parentA = AddNode(model, "ParentA", model.Root);
            var parentB = AddNode(model, "ParentB", model.Root);
            var source = AddNode(model, "Source", parentA);
            var childB1 = AddNode(model, "ChildB1", parentB);
            AddLink(model, source, childB1);
            AddLink(model, source, parentB); // Ends at the target container itself
            model.Zoom = OneLevelZoom;
        }

        service.SplitLine(LineId.From("ParentA", "ParentB"));

        using var model2 = modelMgr.UseModel();
        var original = model2.Lines[LineId.From("ParentA", "ParentB")];
        Assert.True(model2.Lines.ContainsKey(LineId.FromDirect("ParentA", "ChildB1")));
        Assert.False(original.IsSplitSuppressed); // Still represents the link to ParentB
    }

    [Fact]
    public void HideDirectLine_ShouldRestoreOriginal_WhenLastSplitLineIsHidden()
    {
        var service = CreateService();
        using (var model = modelMgr.UseModel())
        {
            var parentA = AddNode(model, "ParentA", model.Root);
            var parentB = AddNode(model, "ParentB", model.Root);
            var source = AddNode(model, "Source", parentA);
            var childB1 = AddNode(model, "ChildB1", parentB);
            var childB2 = AddNode(model, "ChildB2", parentB);
            AddLink(model, source, childB1);
            AddLink(model, source, childB2);
            model.Zoom = OneLevelZoom;
        }

        service.SplitLine(LineId.From("ParentA", "ParentB"));
        service.HideDirectLine(LineId.FromDirect("ParentA", "ChildB1"));

        using (var model = modelMgr.UseModel())
        {
            var original = model.Lines[LineId.From("ParentA", "ParentB")];
            Assert.True(original.IsSplitSuppressed); // One split line remains
            Assert.Single(original.SplitLines);
        }

        service.HideDirectLine(LineId.FromDirect("ParentA", "ChildB2"));

        using (var model = modelMgr.UseModel())
        {
            var original = model.Lines[LineId.From("ParentA", "ParentB")];
            Assert.False(original.IsSplitSuppressed); // Last split line hidden; original returns
            Assert.Empty(original.SplitLines);

            // The hidden split lines are fully detached from the links
            foreach (var link in original.Links)
            {
                Assert.DoesNotContain(link.Lines, l => l.IsDirect);
            }
        }
    }

    [Fact]
    public void SplitLine_ShouldBeSplittableAgain_WhenIntermediateWasCollapsed()
    {
        var service = CreateService();
        using (var model = modelMgr.UseModel())
        {
            var parentA = AddNode(model, "ParentA", model.Root);
            var parentB = AddNode(model, "ParentB", model.Root);
            var source = AddNode(model, "Source", parentA);
            var childB1 = AddNode(model, "ChildB1", parentB);
            var target1 = AddNode(model, "Target1", childB1);
            AddLink(model, source, target1);
            model.Zoom = OneLevelZoom; // ChildB1 is an icon, so the first split stops at it
        }

        service.SplitLine(LineId.From("ParentA", "ParentB"));

        var splitLineId = LineId.FromDirect("ParentA", "ChildB1");
        Assert.True(service.CanSplitLine(splitLineId)); // Its link goes deeper than ChildB1

        service.SplitLine(splitLineId);

        using var model2 = modelMgr.UseModel();
        var firstSplit = model2.Lines[splitLineId];
        var secondSplit = model2.Lines[LineId.FromDirect("ParentA", "Target1")];
        Assert.Same(firstSplit, secondSplit.SplitParent);
        Assert.True(firstSplit.IsSplitSuppressed);
        Assert.False(service.CanSplitLine(secondSplit.Id)); // The link ends at Target1
    }

    [Fact]
    public void CanSplitLine_ShouldBeFalse_WhenAllLinksEndAtTarget()
    {
        var service = CreateService();
        using (var model = modelMgr.UseModel())
        {
            var parentA = AddNode(model, "ParentA", model.Root);
            var parentB = AddNode(model, "ParentB", model.Root);
            var source = AddNode(model, "Source", parentA);
            AddLink(model, source, parentB);
            model.Zoom = OneLevelZoom;
        }

        Assert.False(service.CanSplitLine(LineId.From("ParentA", "ParentB")));
    }

    [Fact]
    public void RemoveLink_ShouldRestoreOriginal_WhenSplitLinesBecomeEmpty()
    {
        var service = CreateService();
        Link link;
        using (var model = modelMgr.UseModel())
        {
            var parentA = AddNode(model, "ParentA", model.Root);
            var parentB = AddNode(model, "ParentB", model.Root);
            var source = AddNode(model, "Source", parentA);
            var childB1 = AddNode(model, "ChildB1", parentB);
            link = AddLink(model, source, childB1);
            model.Zoom = OneLevelZoom;
        }

        service.SplitLine(LineId.From("ParentA", "ParentB"));

        using (var model = modelMgr.UseModel())
        {
            var original = model.Lines[LineId.From("ParentA", "ParentB")];
            Assert.True(original.IsSplitSuppressed);

            // A re-parse removing the link tears down the empty split line and releases the
            // original (which is itself removed too, as its last link is gone)
            model.RemoveLink(link);
            Assert.False(model.Lines.ContainsKey(LineId.FromDirect("ParentA", "ChildB1")));
            Assert.False(original.IsSplitSuppressed);
        }
    }

    static Node AddNode(IModel model, string name, Node parent)
    {
        var node = new Node(name, parent);
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static Link AddLink(IModel model, Node source, Node target)
    {
        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
        return link;
    }
}
