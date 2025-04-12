namespace Dependinator.Models;

interface ICommandService
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    void Do(IModel model, Command command);
    void Redo(Func<IModel> useModel);
    void Undo(Func<IModel> useModel);
}

[Scoped]
class CommandService : ICommandService
{
    readonly IApplicationEvents applicationEvents;
    readonly Stack<Command> undoStack = [];
    readonly Stack<Command> redoStack = [];

    public CommandService(IApplicationEvents applicationEvents)
    {
        this.applicationEvents = applicationEvents;
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
        applicationEvents.TriggerUndoneRedone();
        applicationEvents.TriggerUIStateChanged();
    }

    public void Undo(Func<IModel> useModel)
    {
        if (!CanUndo)
            return;

        if (undoStack.Peek() is CompositeCommand composite)
        {
            UndoComposite(useModel, composite);
            return;
        }
        var command = undoStack.Pop();
        redoStack.Push(command);

        using (var model = useModel())
        {
            command.Unexecute(model);
            model.ClearCachedSvg();
        }
        applicationEvents.TriggerUndoneRedone();
        applicationEvents.TriggerUIStateChanged();
    }

    public void Redo(Func<IModel> useModel)
    {
        if (!CanRedo)
            return;

        if (redoStack.Peek() is CompositeCommand composite)
        {
            RedoComposite(useModel, composite);
            return;
        }

        var command = redoStack.Pop();
        undoStack.Push(command);
        using (var model = useModel())
        {
            command.Execute(model);
        }

        applicationEvents.TriggerUndoneRedone();
        applicationEvents.TriggerUIStateChanged();
    }

    async void UndoComposite(Func<IModel> useModel, CompositeCommand composite)
    {
        foreach (var subCommand in composite.commands.AsEnumerable().Reverse())
        {
            using (var model = useModel())
            {
                subCommand.Unexecute(model);
                model.ClearCachedSvg();
            }

            applicationEvents.TriggerUndoneRedone();
            applicationEvents.TriggerUIStateChanged();
            await Task.Delay(2);
        }

        var command = undoStack.Pop();
        redoStack.Push(command);
    }

    async void RedoComposite(Func<IModel> useModel, CompositeCommand composite)
    {
        foreach (var subCommand in composite.commands)
        {
            using (var model = useModel())
            {
                subCommand.Execute(model);
                model.ClearCachedSvg();
            }

            applicationEvents.TriggerUndoneRedone();
            applicationEvents.TriggerUIStateChanged();
            await Task.Delay(2);
        }

        var command = redoStack.Pop();
        undoStack.Push(command);
    }
}
