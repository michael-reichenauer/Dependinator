using Dependinator.UI.Modeling.Models;

// Undoable edit commands for the model (node, line, and composite edits) with an undo/redo stack.
namespace Dependinator.UI.Modeling.Commands;

interface ICommandService
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    void Do(Command command, bool isClearCache = true, bool isSaveModel = true);
    Task Redo();
    Task Undo();
}

[Scoped]
class CommandService(IApplicationEvents applicationEvents, IModelMgr modelMgr) : ICommandService
{
    // Delay between sub-commands when replaying a composite, so the user sees the steps.
    static readonly TimeSpan replayStepDelay = TimeSpan.FromMilliseconds(4);

    readonly Stack<Command> undoStack = [];
    readonly Stack<Command> redoStack = [];
    bool isReplaying; // Blocks Undo/Redo while a composite replay is in progress

    public bool CanUndo => !isReplaying && undoStack.Count > 0;
    public bool CanRedo => !isReplaying && redoStack.Count > 0;

    public void Do(Command command, bool isClearCache = true, bool isSaveModel = true)
    {
        ExecuteAndPush(command);
        Notify(isModelChanged: isClearCache, isSaveNeeded: isSaveModel);
    }

    public Task Undo() => ReplayAsync(undoStack, redoStack, (command, model) => command.Revert(model));

    public Task Redo() => ReplayAsync(redoStack, undoStack, (command, model) => command.Execute(model));

    void ExecuteAndPush(Command command)
    {
        using (var model = modelMgr.UseModel())
        {
            command.Execute(model);
        }

        // Merge with the previous command if possible (e.g. the stream of small edits produced
        // while dragging a node), so a single undo reverts the whole gesture.
        if (undoStack.Count > 0)
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

                undoStack.Pop();
                command = new CompositeCommand(previous, command);
            }
        }

        undoStack.Push(command);
        redoStack.Clear();
    }

    // Moves the top command from one stack to the other, applying Revert (undo) or Execute
    // (redo). A composite is replayed one sub-command at a time with a short delay, so the user
    // sees the steps; the stacks are updated up front so a re-entrant Undo/Redo cannot replay
    // the same command twice.
    async Task ReplayAsync(Stack<Command> from, Stack<Command> to, Action<Command, IModel> apply)
    {
        if (isReplaying || from.Count == 0)
            return;

        var command = from.Pop();
        to.Push(command);

        if (command is not CompositeCommand composite)
        {
            Apply(command, apply);
            return;
        }

        isReplaying = true;
        try
        {
            // Undo reverts sub-commands in reverse order; CompositeCommand.Revert does the same.
            var subCommands = from == undoStack ? composite.Commands.Reverse() : composite.Commands;
            foreach (var subCommand in subCommands)
            {
                await using var _ = new MinDelay(replayStepDelay);
                Apply(subCommand, apply, isSaveNeeded: false);
            }
        }
        finally
        {
            isReplaying = false;
            Notify(); // Re-enables undo/redo and saves once for the whole replay
        }
    }

    void Apply(Command command, Action<Command, IModel> apply, bool isSaveNeeded = true)
    {
        using (var model = modelMgr.UseModel())
        {
            apply(command, model);
        }

        Notify(isSaveNeeded: isSaveNeeded);
    }

    void Notify(bool isModelChanged = true, bool isSaveNeeded = true)
    {
        if (isModelChanged)
            applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUndoneRedone();
        applicationEvents.TriggerUIStateChanged();
        if (isSaveNeeded)
            applicationEvents.TriggerSaveNeeded();
    }
}
