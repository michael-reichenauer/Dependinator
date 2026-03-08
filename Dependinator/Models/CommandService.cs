namespace Dependinator.Models;

interface ICommandService
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    void Do(Command command, bool isClearCache = true);
    void Redo();
    void Undo();
}

[Scoped]
class CommandService(IApplicationEvents applicationEvents, ITilesMgr tilesMgr, IModelMgr modelMgr) : ICommandService
{
    readonly Stack<Command> undoStack = [];
    readonly Stack<Command> redoStack = [];

    public bool CanUndo => undoStack.Any();
    public bool CanRedo => redoStack.Any();

    public void Do(Command command, bool isClearCache = true)
    {
        modelMgr.WithModel(m => command.Execute(m));
        if (isClearCache)
            tilesMgr.ClearCache();

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
        applicationEvents.TriggerUndoneRedone();
        applicationEvents.TriggerUIStateChanged();
        applicationEvents.TriggerSaveNeeded();
    }

    public void Undo()
    {
        if (!CanUndo)
            return;

        if (undoStack.Peek() is CompositeCommand composite)
        {
            UndoComposite(composite);
            return;
        }
        var command = undoStack.Pop();
        redoStack.Push(command);

        using (var model = modelMgr.UseModel())
        {
            command.Revert(model);
        }
        tilesMgr.ClearCache();
        applicationEvents.TriggerUndoneRedone();
        applicationEvents.TriggerUIStateChanged();
        applicationEvents.TriggerSaveNeeded();
    }

    public void Redo()
    {
        if (!CanRedo)
            return;

        if (redoStack.Peek() is CompositeCommand composite)
        {
            RedoComposite(composite);
            return;
        }

        var command = redoStack.Pop();
        undoStack.Push(command);
        using (var model = modelMgr.UseModel())
        {
            command.Execute(model);
        }

        applicationEvents.TriggerUndoneRedone();
        applicationEvents.TriggerUIStateChanged();
        applicationEvents.TriggerSaveNeeded();
    }

    async void UndoComposite(CompositeCommand composite)
    {
        foreach (var subCommand in composite.commands.AsEnumerable().Reverse())
        {
            using (var model = modelMgr.UseModel())
            {
                subCommand.Revert(model);
            }
            tilesMgr.ClearCache();

            applicationEvents.TriggerUndoneRedone();
            applicationEvents.TriggerUIStateChanged();
            await Task.Delay(2);
        }

        var command = undoStack.Pop();
        redoStack.Push(command);
    }

    async void RedoComposite(CompositeCommand composite)
    {
        foreach (var subCommand in composite.commands)
        {
            using (var model = modelMgr.UseModel())
            {
                subCommand.Execute(model);
            }
            tilesMgr.ClearCache();

            applicationEvents.TriggerUndoneRedone();
            applicationEvents.TriggerUIStateChanged();
            await Task.Delay(2);
        }

        var command = redoStack.Pop();
        undoStack.Push(command);
    }
}
