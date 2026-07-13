using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

abstract class Command
{
    static readonly TimeSpan compositeTimeout = TimeSpan.FromMilliseconds(500);

    public virtual DateTime TimeStamp { get; } = DateTime.UtcNow;
    public virtual string Type => this.GetType().Name;

    // Identifies the edit target (e.g. node or line id); "" when the command has no specific
    // target. Commands with different keys never merge, so rapid edits to different nodes
    // remain separate undo steps.
    public virtual string MergeKey => "";

    public abstract void Execute(IModel model);
    public abstract void Revert(IModel model);

    // Whether this command can merge with the previous one into a single undo step, e.g. the
    // stream of small edits produced while dragging a node.
    public bool CanCombineWith(Command other)
    {
        return Type == other.Type && MergeKey == other.MergeKey && TimeStamp - other.TimeStamp < compositeTimeout;
    }
}
