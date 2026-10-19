using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Changes the accepted cycles in one undo step: accepting one (replacing the earlier acceptances
// it covers), editing a reason (the old entry replaced by the new), or removing some.
class AcceptedCycleCommand(IReadOnlyList<AcceptedCycle> added, IReadOnlyList<AcceptedCycle> removed) : Command
{
    public override void Execute(IModel model)
    {
        foreach (var accepted in removed)
            model.RemoveAcceptedCycle(accepted);
        foreach (var accepted in added)
            model.AddAcceptedCycle(accepted);
    }

    public override void Revert(IModel model)
    {
        foreach (var accepted in added)
            model.RemoveAcceptedCycle(accepted);
        foreach (var accepted in removed)
            model.AddAcceptedCycle(accepted);
    }
}
