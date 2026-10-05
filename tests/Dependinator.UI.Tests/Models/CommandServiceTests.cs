using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

// The undo stack's "undo only if still the latest step": a snackbar's Undo must revert the
// deletion it announced, never an edit made after it.
public class CommandServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly CommandService service;

    public CommandServiceTests()
    {
        service = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);
    }

    Node AddRootNode(string name)
    {
        using var model = modelMgr.UseModel();
        var node = new Node(name, model.Root) { Boundary = new Rect(100, 100, 100, 100) };
        model.Root.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    [Fact]
    public async Task UndoIfLatest_ShouldUndoTheCommand_OnlyWhileItIsTheLatestStep()
    {
        var a = AddRootNode("A");
        var b = AddRootNode("B");

        var deleteA = new DeleteNodeCommand(a.Id);
        service.Do(deleteA);
        Assert.False(modelMgr.WithModel(m => m.Nodes.ContainsKey(a.Id)));

        // Something else happened since: the snackbar's Undo must leave it alone.
        await Task.Delay(600); // Past the merge window, so the two commands stay separate steps
        service.Do(new NodeEditCommand(b.Id) { Boundary = b.Boundary with { X = 200 } });
        Assert.False(await service.UndoIfLatest(deleteA));
        Assert.Equal(200, b.Boundary.X);
        Assert.False(modelMgr.WithModel(m => m.Nodes.ContainsKey(a.Id)));

        await service.Undo();
        Assert.True(await service.UndoIfLatest(deleteA));
        Assert.True(modelMgr.WithModel(m => m.Nodes.ContainsKey(a.Id)));
        Assert.False(service.CanUndo);
    }

    [Fact]
    public async Task UndoIfLatest_ShouldUndoTheMergedStep_WhenTheCommandWasFoldedIntoIt()
    {
        var a = AddRootNode("A");
        var b = AddRootNode("B");

        var deleteA = new DeleteNodeCommand(a.Id);
        service.Do(deleteA);
        service.Do(new DeleteNodeCommand(b.Id)); // Same type right after: merged into one undo step

        Assert.True(await service.UndoIfLatest(deleteA));
        Assert.True(modelMgr.WithModel(m => m.Nodes.ContainsKey(a.Id) && m.Nodes.ContainsKey(b.Id)));
        Assert.False(service.CanUndo);
    }
}
