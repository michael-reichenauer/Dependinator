using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Adds the rule; a dismissal of it (the user said no to it before, or removed it) goes, since the
// user wants it now. Undo brings the dismissal back.
class AddRuleCommand(ArchitectureRule rule) : Command
{
    DismissedRule? removedDismissal;

    public override void Execute(IModel model)
    {
        removedDismissal = model.DismissedRules.FirstOrDefault(d => d.IsFor(rule.FromName, rule.ToName));
        if (removedDismissal is not null)
            model.RemoveDismissedRule(removedDismissal);
        model.AddRule(rule);
    }

    public override void Revert(IModel model)
    {
        model.RemoveRule(rule);
        if (removedDismissal is not null)
            model.AddDismissedRule(removedDismissal);
    }
}

// Removes the rule with the dependencies allowed under it, and remembers it as dismissed, so
// Suggest rules does not offer it again; undo brings them back together.
class RemoveRuleCommand(ArchitectureRule rule) : Command
{
    public const string DismissReason = "Removed from the rules.";

    IReadOnlyList<AllowedViolation> removedAllowed = [];
    ArchitectureRule removed = rule;
    DismissedRule? replacedDismissal;

    public override void Execute(IModel model)
    {
        removed = model.Rules.FirstOrDefault(r => r == rule) ?? rule;
        removedAllowed = [.. model.AllowedViolations.Where(a => a.IsFor(rule))];
        foreach (var allowed in removedAllowed)
            model.RemoveAllowedViolation(allowed);
        model.RemoveRule(rule);
        replacedDismissal = model.DismissedRules.FirstOrDefault(d => d.IsFor(rule.FromName, rule.ToName));
        model.AddDismissedRule(new DismissedRule(rule.FromName, rule.ToName, DismissReason));
    }

    public override void Revert(IModel model)
    {
        model.RemoveDismissedRule(new DismissedRule(rule.FromName, rule.ToName));
        if (replacedDismissal is not null)
            model.AddDismissedRule(replacedDismissal);
        model.AddRule(removed);
        foreach (var allowed in removedAllowed)
            model.AddAllowedViolation(allowed);
    }
}

// Changes why a rule exists.
class SetRuleReasonCommand(ArchitectureRule before, ArchitectureRule after) : Command
{
    public override void Execute(IModel model) => model.ReplaceRule(after);

    public override void Revert(IModel model) => model.ReplaceRule(before);
}

// Dismisses rules (not to be suggested again), or takes dismissals back, in one undoable step.
class DismissRuleCommand(IReadOnlyList<DismissedRule> added, IReadOnlyList<DismissedRule> removed) : Command
{
    IReadOnlyList<DismissedRule> replaced = [];

    public override void Execute(IModel model)
    {
        replaced = [.. added.Select(a => model.DismissedRules.FirstOrDefault(d => d == a)).OfType<DismissedRule>()];
        foreach (var dismissed in removed)
            model.RemoveDismissedRule(dismissed);
        foreach (var dismissed in added)
            model.AddDismissedRule(dismissed);
    }

    public override void Revert(IModel model)
    {
        foreach (var dismissed in added)
            model.RemoveDismissedRule(dismissed);
        foreach (var dismissed in replaced)
            model.AddDismissedRule(dismissed);
        foreach (var dismissed in removed)
            model.AddDismissedRule(dismissed);
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
