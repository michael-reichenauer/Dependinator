using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

class AddRuleCommand(ArchitectureRule rule) : Command
{
    public override void Execute(IModel model) => model.AddRule(rule);

    public override void Revert(IModel model) => model.RemoveRule(rule);
}

// Removes the rule with the dependencies allowed under it; undo brings them back together.
class RemoveRuleCommand(ArchitectureRule rule) : Command
{
    IReadOnlyList<AllowedViolation> removedAllowed = [];

    public override void Execute(IModel model)
    {
        removedAllowed = [.. model.AllowedViolations.Where(a => a.IsFor(rule))];
        foreach (var allowed in removedAllowed)
            model.RemoveAllowedViolation(allowed);
        model.RemoveRule(rule);
    }

    public override void Revert(IModel model)
    {
        model.AddRule(rule);
        foreach (var allowed in removedAllowed)
            model.AddAllowedViolation(allowed);
    }
}

// Allows dependencies that break a rule (with why), or stops allowing them, in one undoable step.
class AllowViolationCommand(IReadOnlyList<AllowedViolation> added, IReadOnlyList<AllowedViolation> removed) : Command
{
    public override void Execute(IModel model)
    {
        foreach (var allowed in removed)
            model.RemoveAllowedViolation(allowed);
        foreach (var allowed in added)
            model.AddAllowedViolation(allowed);
    }

    public override void Revert(IModel model)
    {
        foreach (var allowed in added)
            model.RemoveAllowedViolation(allowed);
        foreach (var allowed in removed)
            model.AddAllowedViolation(allowed);
    }
}
