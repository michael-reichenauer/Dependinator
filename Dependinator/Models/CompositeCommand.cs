namespace Dependinator.Models;

class CompositeCommand : Command
{
    readonly List<Command> commands = [];
    readonly string typeName;
    DateTime timeStamp;

    public override string Type => typeName;
    public override DateTime TimeStamp => timeStamp;

    public CompositeCommand(params Command[] commands)
    {
        typeName = commands.First().Type;
        timeStamp = commands.Last().TimeStamp;
        this.commands.Add(commands);
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

    public override void Unexecute(IModel model)
    {
        foreach (var command in commands.AsEnumerable().Reverse())
        {
            command.Unexecute(model);
        }
    }
}
