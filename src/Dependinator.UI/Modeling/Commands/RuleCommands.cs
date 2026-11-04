using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Adds the rule; a dismissal of it (the user said no to it before, or removed it) goes, since the
// user wants it now. Undo brings the dismissal back.
class AddRuleCommand(ArchitectureRule rule) : Command
{
    DismissedRule? removedDismissal;

    public override void Execute(IModel model)
    {
        removedDismissal =
            rule.Kind == RuleKind.MustNotUse
                ? model.DismissedRules.FirstOrDefault(d => d.IsFor(rule.FromName, rule.ToName))
                : null;
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

    // The removed rule's own why stays with its dismissal, for the user and the next prompt.
    public static string DismissReasonFor(ArchitectureRule rule) =>
        rule.Reason == "" ? DismissReason : $"{DismissReason} {rule.Reason}";

    IReadOnlyList<AllowedViolation> removedAllowed = [];
    ArchitectureRule? removed;
    DismissedRule? replacedDismissal;

    public override void Execute(IModel model)
    {
        removed = model.Rules.FirstOrDefault(r => r == rule) ?? rule;
        removedAllowed = [.. model.AllowedViolations.Where(a => a.IsFor(rule))];
        foreach (var allowed in removedAllowed)
            model.RemoveAllowedViolation(allowed);
        model.RemoveRule(rule);
        // Only a "must not use" rule is ever suggested, so only it is remembered as dismissed.
        if (rule.Kind != RuleKind.MustNotUse)
            return;
        replacedDismissal = model.DismissedRules.FirstOrDefault(d => d.IsFor(rule.FromName, rule.ToName));
        model.AddDismissedRule(new DismissedRule(rule.FromName, rule.ToName, DismissReasonFor(removed)));
    }

    public override void Revert(IModel model)
    {
        if (rule.Kind == RuleKind.MustNotUse)
            model.RemoveDismissedRule(new DismissedRule(rule.FromName, rule.ToName));
        if (replacedDismissal is not null)
            model.AddDismissedRule(replacedDismissal);
        model.AddRule(removed ?? rule);
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
    IReadOnlyList<DismissedRule> actuallyRemoved = [];

    public override void Execute(IModel model)
    {
        replaced = [.. added.Select(a => model.DismissedRules.FirstOrDefault(d => d == a)).OfType<DismissedRule>()];
        // The stored dismissals (with their reasons), and only those that are there, so undo
        // brings back what was removed and nothing else.
        actuallyRemoved =
        [
            .. removed.Select(r => model.DismissedRules.FirstOrDefault(d => d == r)).OfType<DismissedRule>(),
        ];
        foreach (var dismissed in actuallyRemoved)
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
        foreach (var dismissed in actuallyRemoved)
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
