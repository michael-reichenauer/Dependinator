using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

class AddRuleCommand(ArchitectureRule rule) : Command
{
    public override void Execute(IModel model) => model.AddRule(rule);

    public override void Revert(IModel model) => model.RemoveRule(rule);
}

class RemoveRuleCommand(ArchitectureRule rule) : Command
{
    public override void Execute(IModel model) => model.RemoveRule(rule);

    public override void Revert(IModel model) => model.AddRule(rule);
}
