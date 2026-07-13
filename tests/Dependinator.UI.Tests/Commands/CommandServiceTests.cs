using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Commands;

// A test double Command that records Execute/Revert calls and lets the test control the
// Type/TimeStamp/MergeKey used by the combine heuristic (Command.CanCombineWith).
class FakeCommand(string type = "Fake", DateTime? timeStamp = null, string mergeKey = "") : Command
{
    public int Executes { get; private set; }
    public int Reverts { get; private set; }

    public override string Type => type;
    public override DateTime TimeStamp => timeStamp ?? base.TimeStamp;
    public override string MergeKey => mergeKey;

    public override void Execute(IModel model) => Executes++;

    public override void Revert(IModel model) => Reverts++;
}

public class CommandServiceTests
{
    static CommandService CreateService()
    {
        var events = new Mock<IApplicationEvents>();
        var modelMgr = new Mock<IModelMgr>();
        modelMgr.Setup(m => m.UseModel()).Returns(Mock.Of<IModel>());
        return new CommandService(events.Object, modelMgr.Object);
    }

    [Fact]
    public void NewService_ShouldHaveNothingToUndoOrRedo()
    {
        var service = CreateService();

        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Do_ShouldExecuteCommandAndEnableUndo()
    {
        var service = CreateService();
        var command = new FakeCommand();

        service.Do(command);

        Assert.Equal(1, command.Executes);
        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public async Task Undo_ShouldRevertCommandAndEnableRedo()
    {
        var service = CreateService();
        var command = new FakeCommand();
        service.Do(command);

        await service.Undo();

        Assert.Equal(1, command.Reverts);
        Assert.False(service.CanUndo);
        Assert.True(service.CanRedo);
    }

    [Fact]
    public async Task Redo_ShouldReExecuteCommandAndEnableUndo()
    {
        var service = CreateService();
        var command = new FakeCommand();
        service.Do(command);
        await service.Undo();

        await service.Redo();

        Assert.Equal(2, command.Executes);
        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public async Task Do_ShouldClearRedoStack()
    {
        var service = CreateService();
        service.Do(new FakeCommand());
        await service.Undo();
        Assert.True(service.CanRedo);

        // A fresh action invalidates the redo history.
        service.Do(new FakeCommand(type: "Other"));

        Assert.False(service.CanRedo);
    }

    [Fact]
    public async Task Undo_ShouldUnwindNonCombinableCommandsOneAtATime()
    {
        var service = CreateService();

        // Different Type means the commands do not combine into a composite.
        service.Do(new FakeCommand(type: "A"));
        service.Do(new FakeCommand(type: "B"));

        await service.Undo();
        Assert.True(service.CanUndo); // first command still on the stack

        await service.Undo();
        Assert.False(service.CanUndo);
    }

    [Fact]
    public async Task Undo_OnEmptyStack_ShouldBeNoOp()
    {
        var service = CreateService();

        await service.Undo();
        await service.Redo();

        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public async Task Undo_DuringCompositeReplay_ShouldNotRevertTwice()
    {
        var service = CreateService();
        var a = new FakeCommand();
        var b = new FakeCommand();
        service.Do(new CompositeCommand(a, b));

        // The composite replays its sub-commands with a small delay between them; a second
        // Undo arriving during the replay must not revert the same composite again.
        var first = service.Undo();
        var second = service.Undo();
        await Task.WhenAll(first, second);

        Assert.Equal(1, a.Reverts);
        Assert.Equal(1, b.Reverts);
        Assert.False(service.CanUndo);
        Assert.True(service.CanRedo);
    }

    [Fact]
    public async Task Redo_DuringCompositeReplay_ShouldNotExecuteTwice()
    {
        var service = CreateService();
        var a = new FakeCommand();
        var b = new FakeCommand();
        service.Do(new CompositeCommand(a, b));
        await service.Undo();

        var first = service.Redo();
        var second = service.Redo();
        await Task.WhenAll(first, second);

        Assert.Equal(2, a.Executes); // once by Do, once by Redo
        Assert.Equal(2, b.Executes);
        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public async Task Do_SameTypeAndTarget_ShouldCombineIntoOneUndoStep()
    {
        var service = CreateService();
        var a = new FakeCommand(type: "Edit", mergeKey: "node1");
        var b = new FakeCommand(type: "Edit", mergeKey: "node1");
        service.Do(a);
        service.Do(b);

        await service.Undo();

        Assert.Equal(1, a.Reverts);
        Assert.Equal(1, b.Reverts);
        Assert.False(service.CanUndo); // both reverted by a single undo step
    }

    [Fact]
    public async Task Do_SameTypeButDifferentTarget_ShouldStaySeparateUndoSteps()
    {
        var service = CreateService();
        var a = new FakeCommand(type: "Edit", mergeKey: "node1");
        var b = new FakeCommand(type: "Edit", mergeKey: "node2");
        service.Do(a);
        service.Do(b);

        await service.Undo();

        Assert.Equal(0, a.Reverts);
        Assert.Equal(1, b.Reverts);
        Assert.True(service.CanUndo); // first command still on the stack
    }
}
