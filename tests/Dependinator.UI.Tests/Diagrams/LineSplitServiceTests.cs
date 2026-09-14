using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Dependencies;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Diagrams;

// The per-node direct-line depth is transient view state: each change bumps the model's
// structure version (so RepLineService re-syncs) and rebuilds the tiles (ModelChanged).
public class LineSplitServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly Mock<IApplicationEvents> applicationEvents = new();
    readonly Mock<IDependenciesService> dependenciesService = new();

    LineSplitService CreateService() => new(modelMgr, applicationEvents.Object, dependenciesService.Object);

    [Fact]
    public void IncreaseDepth_ShouldAddOneLevel_AndInvalidateLines()
    {
        var container = AddNodes(childLevels: 2);
        var version = modelMgr.WithModel(m => m.StructureVersion);
        var service = CreateService();

        service.IncreaseDepth(container.Id);

        Assert.Equal(1, service.GetDepth(container.Id));
        Assert.True(service.HasSplits(container.Id));
        Assert.Equal(version + 1, modelMgr.WithModel(m => m.StructureVersion));
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Once);
        applicationEvents.Verify(e => e.TriggerUIStateChanged(), Times.Once);
    }

    [Fact]
    public void IncreaseDepth_ShouldClampAtSubtreeHeight()
    {
        var container = AddNodes(childLevels: 2);
        var service = CreateService();

        service.IncreaseDepth(container.Id);
        service.IncreaseDepth(container.Id);
        service.IncreaseDepth(container.Id);

        Assert.Equal(2, service.GetDepth(container.Id));
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Exactly(2)); // The third click is a no-op
    }

    [Fact]
    public void IncreaseDepth_ShouldNotCountPassThroughNodesAsLevels()
    {
        var container = AddNodes(childLevels: 2);
        modelMgr.WithModel(m => m.Nodes[NodeId.FromName("Child1")].IsPassThrough = true);
        var service = CreateService();

        service.IncreaseDepth(container.Id);
        service.IncreaseDepth(container.Id);

        Assert.Equal(1, service.GetDepth(container.Id));
    }

    [Fact]
    public void DecreaseDepth_ShouldStopAtZero()
    {
        var container = AddNodes(childLevels: 2);
        var service = CreateService();
        service.IncreaseDepth(container.Id);
        applicationEvents.Invocations.Clear();

        service.DecreaseDepth(container.Id);
        service.DecreaseDepth(container.Id);

        Assert.Equal(0, service.GetDepth(container.Id));
        Assert.False(service.HasSplits(container.Id));
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Once); // The second click is a no-op
    }

    [Fact]
    public void Clear_ShouldResetNodeAndDescendants()
    {
        var container = AddNodes(childLevels: 2);
        var child = NodeId.FromName("Child1");
        var other = AddNodes(childLevels: 1, prefix: "Other");
        var service = CreateService();
        service.IncreaseDepth(container.Id);
        service.IncreaseDepth(child);
        service.IncreaseDepth(other.Id);
        applicationEvents.Invocations.Clear();

        service.Clear(container.Id);

        Assert.Equal(0, service.GetDepth(container.Id));
        Assert.Equal(0, service.GetDepth(child));
        Assert.Equal(1, service.GetDepth(other.Id));
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Once);

        // Nothing left to clear: no change, no re-render
        service.Clear(container.Id);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Once);
    }

    [Fact]
    public void ClearAll_ShouldResetEveryNode()
    {
        var container = AddNodes(childLevels: 2);
        var other = AddNodes(childLevels: 1, prefix: "Other");
        var service = CreateService();
        service.IncreaseDepth(container.Id);
        service.IncreaseDepth(other.Id);

        service.ClearAll();

        Assert.Equal(0, service.GetDepth(container.Id));
        Assert.Equal(0, service.GetDepth(other.Id));
        // The explorer's lines are on-demand lines too
        dependenciesService.Verify(d => d.SetShowLines(false), Times.Once);
    }

    [Fact]
    public void IncreaseDepth_ShouldIgnoreUnknownNode()
    {
        AddNodes(childLevels: 1);
        var service = CreateService();

        service.IncreaseDepth(NodeId.FromName("Missing"));

        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Never);
    }

    // A top-level container with a single chain of descendants: Container -> Child1 -> Child2 ...
    Node AddNodes(int childLevels, string prefix = "")
    {
        using var model = modelMgr.UseModel();
        var container = AddNode(model, $"{prefix}Container", model.Root);
        var parent = container;
        for (int level = 1; level <= childLevels; level++)
        {
            parent = AddNode(model, $"{prefix}Child{level}", parent);
        }
        return container;
    }

    static Node AddNode(IModel model, string name, Node parent)
    {
        var node = new Node(name, parent);
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }
}
