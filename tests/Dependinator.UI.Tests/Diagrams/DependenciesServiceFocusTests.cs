using Dependinator.UI.Diagrams.Dependencies;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Diagrams;

// The explorer mirrors its state into Model.LineFocus while its lines are shown: the subject
// and direction, and the far-side rows currently expanded. Every change bumps the structure
// version (RepLineService re-syncs) and rebuilds the tiles (ModelChanged).
public class DependenciesServiceFocusTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly Mock<ISelectionService> selectionService = new();
    readonly Mock<IApplicationEvents> applicationEvents = new();
    readonly Mock<INavigationService> navigationService = new();

    Node source = null!;
    Node target = null!;
    Node parentA = null!;
    Node parentB = null!;

    DependenciesService CreateService(PointerId selected)
    {
        selectionService.Setup(s => s.SelectedId).Returns(selected);
        return new(selectionService.Object, applicationEvents.Object, modelMgr, navigationService.Object);
    }

    // Root -> ParentA -> Source, Root -> ParentB -> Target, one link Source -> Target.
    void AddModel()
    {
        using var model = modelMgr.UseModel();
        parentA = AddNode(model, "ParentA", model.Root);
        parentB = AddNode(model, "ParentB", model.Root);
        source = AddNode(model, "Source", parentA);
        target = AddNode(model, "Target", parentB);
        AddLink(model, source, target);
    }

    LineFocus? Focus => modelMgr.WithModel(m => m.LineFocus);
    int Version => modelMgr.WithModel(m => m.StructureVersion);

    [Fact]
    public void ShowDependencies_ShouldFocusSelectedNode()
    {
        AddModel();
        var service = CreateService(PointerId.FromNode(source.Id));
        var version = Version;

        service.ShowDependencies();

        var focus = Focus;
        Assert.NotNull(focus);
        Assert.Same(source, focus!.NearNode);
        Assert.Null(focus.FarNode);
        Assert.Null(focus.Links);
        Assert.False(focus.IsReferences);
        Assert.Empty(focus.ExpandedFarNodes);
        Assert.Equal(version + 1, Version);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Once);
    }

    [Fact]
    public void ShowReferences_ShouldFocusInReferencesDirection()
    {
        AddModel();
        var service = CreateService(PointerId.FromNode(target.Id));

        service.ShowReferences();

        Assert.True(Focus!.IsReferences);
        Assert.Same(target, Focus!.NearNode);
    }

    [Fact]
    public void ShowDependencies_ForSelectedLine_ShouldFocusItsLinks()
    {
        AddModel();
        var line = modelMgr.WithModel(m => m.Lines[LineId.From("ParentA", "ParentB")]);
        var service = CreateService(PointerId.FromLine(line.Id));

        service.ShowDependencies();

        var focus = Focus!;
        Assert.Same(parentA, focus.NearNode);
        Assert.Same(parentB, focus.FarNode);
        Assert.Equal(line.Links.ToHashSet(), focus.Links!);
    }

    [Fact]
    public void SetExpanded_ShouldAddAndRemoveFarNode()
    {
        AddModel();
        var service = CreateService(PointerId.FromNode(source.Id));
        service.ShowDependencies();
        var row = Assert.Single(service.TreeItems); // The far top container ParentB
        Assert.Equal(parentB.Id, row.NodeId);
        applicationEvents.Invocations.Clear();

        service.SetExpanded(row, true);
        Assert.True(row.Expanded);
        Assert.Equal([parentB], Focus!.ExpandedFarNodes);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Once);
        applicationEvents.Verify(e => e.TriggerUIStateChanged(), Times.Once); // Repaints the canvas

        service.SetExpanded(row, false);
        Assert.Empty(Focus!.ExpandedFarNodes);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Exactly(2));

        // Unchanged state is a no-op, also when the tree view already wrote it into the item
        // (MudTreeView does so before raising ExpandedChanged)
        row.Expanded = true;
        service.SetExpanded(row, true);
        Assert.Equal([parentB], Focus!.ExpandedFarNodes);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Exactly(3));
        service.SetExpanded(row, true);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Exactly(3));
    }

    [Fact]
    public void ToggleExpandAll_ShouldExpandWholeBranchIntoFocus()
    {
        AddModel();
        var service = CreateService(PointerId.FromNode(source.Id));
        service.ShowDependencies();
        var row = Assert.Single(service.TreeItems);

        service.ToggleExpandAll(row);

        Assert.Contains(parentB, Focus!.ExpandedFarNodes);
        Assert.Contains(target, Focus!.ExpandedFarNodes);
    }

    [Fact]
    public void Close_ShouldClearFocus()
    {
        AddModel();
        var service = CreateService(PointerId.FromNode(source.Id));
        service.ShowDependencies();
        var version = Version;

        service.Close();

        Assert.Null(Focus);
        Assert.Equal(version + 1, Version);
    }

    [Fact]
    public void Clicked_OnOtherElement_ShouldMinimizeAndKeepFocus()
    {
        AddModel();
        var service = CreateService(PointerId.FromNode(source.Id));
        service.ShowDependencies();

        service.Clicked(PointerId.FromNode(target.Id));

        Assert.True(service.IsShowExplorer);
        Assert.True(service.IsMinimized);
        Assert.NotNull(Focus);
        Assert.Single(service.TreeItems);
    }

    [Fact]
    public void Clicked_OnSubject_ShouldNotMinimize()
    {
        AddModel();
        var service = CreateService(PointerId.FromNode(source.Id));
        service.ShowDependencies();

        service.Clicked(PointerId.FromNode(source.Id));

        Assert.False(service.IsMinimized);
    }

    [Fact]
    public void SetMinimized_ShouldRestore_AndShowResetsIt()
    {
        AddModel();
        var service = CreateService(PointerId.FromNode(source.Id));
        service.ShowDependencies();
        service.Clicked(PointerId.FromNode(target.Id));
        applicationEvents.Invocations.Clear();

        service.SetMinimized(false);
        Assert.False(service.IsMinimized);
        applicationEvents.Verify(e => e.TriggerUIStateChanged(), Times.Once);

        // Unchanged state is a no-op; a fresh Show starts restored; Close resets it
        service.SetMinimized(false);
        applicationEvents.Verify(e => e.TriggerUIStateChanged(), Times.Once);
        service.Clicked(PointerId.FromNode(target.Id));
        service.ShowReferences();
        Assert.False(service.IsMinimized);
        service.Clicked(PointerId.FromNode(target.Id));
        service.Close();
        Assert.False(service.IsMinimized);
        service.SetMinimized(true); // Closed: nothing to minimize
        Assert.False(service.IsMinimized);
    }

    [Fact]
    public void SetShowLines_ShouldClearAndRestoreFocus_WhileKeepingTree()
    {
        AddModel();
        var service = CreateService(PointerId.FromNode(source.Id));
        service.ShowDependencies();

        service.SetShowLines(false);
        Assert.Null(Focus);
        Assert.True(service.IsShowExplorer);
        Assert.Single(service.TreeItems);

        service.SetShowLines(true);
        Assert.NotNull(Focus);
    }

    [Fact]
    public void Show_WhenLinesAreOff_ShouldNotSetFocus()
    {
        AddModel();
        var service = CreateService(PointerId.FromNode(source.Id));
        service.SetShowLines(false);
        applicationEvents.Invocations.Clear();

        service.ShowDependencies();

        Assert.Null(Focus);
        Assert.True(service.IsShowExplorer);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Never);
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
