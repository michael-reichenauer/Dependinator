using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Models;

// Verifies that manually added (user-drawn) nodes and links survive the stale-item removal that
// runs after each re-parse, and that manual links dangling on a removed node are cleaned up.
public class ManualNodeReconciliationTests
{
    static StructureService CreateStructureService() => new(new Mock<ILineService>().Object);

    [Fact]
    public void ClearNotUpdated_ShouldKeepManualNode_EvenWhenStale()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var structureService = CreateStructureService();
        var stamp = new DateTime(2024, 1, 1);
        model.UpdateStamp = stamp;

        // A manual node is always "stale" by stamp (parsing never re-stamps it).
        var manual = new Node("Manual", model.Root) { IsManual = true, UpdateStamp = stamp.AddDays(-1) };
        model.Root.AddChild(manual);
        model.TryAddNode(manual);

        structureService.ClearNotUpdated(model);

        Assert.True(model.Nodes.ContainsKey(NodeId.FromName("Manual")));
    }

    [Fact]
    public void ClearNotUpdated_ShouldKeepManualLink_BetweenLiveNodes()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var structureService = CreateStructureService();
        var stamp = new DateTime(2024, 1, 1);
        model.UpdateStamp = stamp;

        AddNode(model, "Source", stamp);
        AddNode(model, "Target", stamp);

        var link = structureService.AddManualLink(model, "Source", "Target");
        Assert.NotNull(link);
        link!.UpdateStamp = stamp.AddDays(-1); // stale by stamp, but manual

        structureService.ClearNotUpdated(model);

        Assert.True(model.Links.ContainsKey(new LinkId("Source", "Target")));
    }

    [Fact]
    public void ClearNotUpdated_ShouldRemoveManualLink_WhenEndpointNodeRemoved()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var structureService = CreateStructureService();
        var stamp = new DateTime(2024, 1, 1);
        model.UpdateStamp = stamp;

        // Source is a parsed node that disappeared this parse (stale) -> it will be removed.
        AddNode(model, "Source", stamp.AddDays(-1));
        AddNode(model, "Target", stamp);

        var link = structureService.AddManualLink(model, "Source", "Target");
        Assert.NotNull(link);

        structureService.ClearNotUpdated(model);

        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("Source")));
        Assert.False(model.Links.ContainsKey(new LinkId("Source", "Target")));
    }

    static void AddNode(IModel model, string name, DateTime stamp)
    {
        var node = new Node(name, model.Root) { UpdateStamp = stamp };
        model.Root.AddChild(node);
        model.TryAddNode(node);
    }
}
