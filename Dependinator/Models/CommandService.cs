namespace Dependinator.Models;


interface ICommandService
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    void Do(IModel model, Command command);
    void Undo(IModel model);
    void Redo(IModel model);
}


[Singleton]
class CommandService : ICommandService
{
    readonly Stack<Command> undoStack = [];
    readonly Stack<Command> redoStack = [];


    public CommandService()
    {
    }


    public bool CanUndo => undoStack.Any();
    public bool CanRedo => redoStack.Any();


    public void Do(IModel model, Command command)
    {
        command.Execute(model);

        // Check if command can be combined with previous command (e.g. multiple edits in a row)
        if (undoStack.Any())
        {
            var previous = undoStack.Peek();
            if (command.CanCombineWith(previous))
            {
                if (previous is CompositeCommand composite)
                {
                    composite.Add(command);
                    redoStack.Clear();
                    return;
                }
                else
                {
                    undoStack.Pop();
                    command = new CompositeCommand(previous, command);
                }
            }
        }

        undoStack.Push(command);
        redoStack.Clear();
    }


    public void Undo(IModel model)
    {
        if (!undoStack.Any()) return;

        var command = undoStack.Pop();
        redoStack.Push(command);
        command.Unexecute(model);
    }


    public void Redo(IModel model)
    {
        if (!redoStack.Any()) return;

        var command = redoStack.Pop();
        undoStack.Push(command);
        command.Execute(model);
    }
}
