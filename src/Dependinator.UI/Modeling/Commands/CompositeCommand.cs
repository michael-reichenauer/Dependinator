using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// A sequence of commands undone/redone as one unit. Created either by CommandService merging
// rapid same-type commands (e.g. a drag gesture), or explicitly for multi-part edits such as
// deleting a node together with its links.
class CompositeCommand : Command
{
    readonly List<Command> commands = [];
    readonly string typeName;
    readonly string mergeKey;
    DateTime timeStamp;

    public IReadOnlyList<Command> Commands => commands;

    public override string Type => typeName;
    public override string MergeKey => mergeKey;
    public override DateTime TimeStamp => timeStamp;

    public CompositeCommand(params Command[] commands)
    {
        // Merged same-type commands keep their type/key so later commands can keep merging.
        // Explicit mixed composites get combined names, which never match a single command.
        typeName = string.Join("+", commands.Select(c => c.Type).Distinct());
        mergeKey = string.Join("+", commands.Select(c => c.MergeKey).Distinct());
        timeStamp = commands.Last().TimeStamp;
        this.commands.AddRange(commands);
    }

    public void Add(Command command)
    {
        commands.Add(command);
        timeStamp = command.TimeStamp;
    }

    public override void Execute(IModel model)
    {
        foreach (var command in commands)
        {
            command.Execute(model);
        }
    }

    public override void Revert(IModel model)
    {
        foreach (var command in commands.AsEnumerable().Reverse())
        {
            command.Revert(model);
        }
    }
}
