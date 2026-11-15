using Dependinator.UI.Modeling.Models;

// Undoable edit commands for the model (node, line, and composite edits) with an undo/redo stack.
namespace Dependinator.UI.Modeling.Commands;

interface ICommandService
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    void Do(Command command, bool isClearCache = true, bool isSaveModel = true);

    // Does the command as part of the undo step that holds the given command, when that is still
    // the latest step (e.g. the lines routed again after a node drag, undone with the drag);
    // otherwise as a step of its own.
    void DoInStepOf(Command step, Command command);
    Task Redo();
    Task Undo();

    // Undoes the given command if it is still the latest step (alone or merged into it), e.g.
    // for a snackbar's Undo that must not revert an edit made since. False when it was not.
    Task<bool> UndoIfLatest(Command command);

    // Forgets every step, for when another model is opened: the steps belong to the old one
    // (a rule command, for one, would write the old model's rule into the new one).
    void Clear();
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

    public void DoInStepOf(Command step, Command command)
    {
        if (undoStack.Count == 0 || !IsInStep(undoStack.Peek(), step))
        {
            Do(command);
            return;
        }

        using (var model = modelMgr.UseModel())
        {
            command.Execute(model);
        }
        var latest = undoStack.Pop();
        if (latest is CompositeCommand composite)
            composite.Add(command);
        else
            latest = new CompositeCommand(latest, command);
        undoStack.Push(latest);
        redoStack.Clear();
        Notify();
    }

    // Whether the command is the step or part of it, nested composites too (a group drag merges
    // composites); the newest parts are looked at first.
    static bool IsInStep(Command step, Command command)
    {
        if (step == command)
            return true;
        if (step is not CompositeCommand composite)
            return false;
        for (var index = composite.Commands.Count - 1; index >= 0; index--)
        {
            if (IsInStep(composite.Commands[index], command))
                return true;
        }
        return false;
    }

    public Task Undo() => ReplayAsync(undoStack, redoStack, (command, model) => command.Revert(model));

    public Task Redo() => ReplayAsync(redoStack, undoStack, (command, model) => command.Execute(model));

    public void Clear()
    {
        if (undoStack.Count == 0 && redoStack.Count == 0)
            return;
        undoStack.Clear();
        redoStack.Clear();
        applicationEvents.TriggerUndoneRedone();
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task<bool> UndoIfLatest(Command command)
    {
        if (undoStack.Count == 0)
            return false;
        if (!IsInStep(undoStack.Peek(), command))
            return false;
        await Undo();
        return true;
    }

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
