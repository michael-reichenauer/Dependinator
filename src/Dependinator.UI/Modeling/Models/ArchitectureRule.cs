namespace Dependinator.UI.Modeling.Models;

// An architecture rule the user declared: nothing at or inside the "from" node may depend on
// anything at or inside the "to" node. Nodes are referred to by name, so a rule survives
// re-parses and travels with the model (ModelDto.Rules); a rule whose node is gone is kept and
// shown as unresolved.
record ArchitectureRule(string FromName, string ToName);
