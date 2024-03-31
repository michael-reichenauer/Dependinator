namespace Dependinator.Models;


interface ICommand
{
    void Execute(IModel model);
    void Unexecute(IModel model);
}


interface ICommandService
{
}


[Singleton]
class CommandService : ICommandService
{
    readonly IModelService modelService;

    readonly Stack<ICommand> undoStack = [];
    readonly Stack<ICommand> redoStack = [];


    public CommandService(IModelService modelService)
    {
        this.modelService = modelService;
    }


    public void Do(Model model, ICommand command)
    {
        command.Execute(model);
        undoStack.Push(command);
        redoStack.Clear();
    }


    public void Undo()
    {
        if (!undoStack.Any()) return;

        var command = undoStack.Pop();
        redoStack.Push(command);

        using var model = modelService.UseModel();
        command.Unexecute(model);
    }


    public void Redo()
    {
        if (!redoStack.Any()) return;

        var command = redoStack.Pop();
        undoStack.Push(command);

        using var model = modelService.UseModel();
        command.Execute(model);
    }
}
