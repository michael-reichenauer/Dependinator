using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Commands;

// A test double Command that records Execute/Revert calls and lets the test control the
// Type/TimeStamp used by the combine heuristic (Command.CanCombineWith).
class FakeCommand(string type = "Fake", DateTime? timeStamp = null) : Command
{
    public int Executes { get; private set; }
    public int Reverts { get; private set; }

    public override string Type => type;
    public override DateTime TimeStamp => timeStamp ?? DateTime.UtcNow;

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
    public void Undo_ShouldRevertCommandAndEnableRedo()
    {
        var service = CreateService();
        var command = new FakeCommand();
        service.Do(command);

        service.Undo();

        Assert.Equal(1, command.Reverts);
        Assert.False(service.CanUndo);
        Assert.True(service.CanRedo);
    }

    [Fact]
    public void Redo_ShouldReExecuteCommandAndEnableUndo()
    {
        var service = CreateService();
        var command = new FakeCommand();
        service.Do(command);
        service.Undo();

        service.Redo();

        Assert.Equal(2, command.Executes);
        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Do_ShouldClearRedoStack()
    {
        var service = CreateService();
        service.Do(new FakeCommand());
        service.Undo();
        Assert.True(service.CanRedo);

        // A fresh action invalidates the redo history.
        service.Do(new FakeCommand(type: "Other"));

        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Undo_ShouldUnwindNonCombinableCommandsOneAtATime()
    {
        var service = CreateService();

        // Different Type means the commands do not combine into a composite.
        service.Do(new FakeCommand(type: "A"));
        service.Do(new FakeCommand(type: "B"));

        service.Undo();
        Assert.True(service.CanUndo); // first command still on the stack

        service.Undo();
        Assert.False(service.CanUndo);
    }

    [Fact]
    public void Undo_OnEmptyStack_ShouldBeNoOp()
    {
        var service = CreateService();

        service.Undo();
        service.Redo();

        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
    }
}
