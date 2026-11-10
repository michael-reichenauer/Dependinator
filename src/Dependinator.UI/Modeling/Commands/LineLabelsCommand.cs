using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Sets the labels of several lines in one step (Suggest Line Labels). Not a CompositeCommand:
// an undo replays a composite one sub-command at a time with a delay, so the user sees the steps
// of a gesture, and forty labels are one step.
class LineLabelsCommand(IReadOnlyList<LineLabelCommand> labels) : Command
{
    readonly IReadOnlyList<LineLabelCommand> labels = labels;

    public IReadOnlyList<LineLabelCommand> Labels => labels;

    // Never merged with another edit.
    public override string MergeKey { get; } = Guid.NewGuid().ToString();

    public override void Execute(IModel model)
    {
        foreach (var label in labels)
            label.Execute(model);
    }

    public override void Revert(IModel model)
    {
        foreach (var label in labels.Reverse())
            label.Revert(model);
    }
}
