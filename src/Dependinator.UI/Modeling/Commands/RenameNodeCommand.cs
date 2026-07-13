using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Renames a node (rebuilds it under the new name, migrating children and links). Revert renames
// back to the original name.
class RenameNodeCommand(IStructureService structureService, string fromName, string toName) : Command
{
    readonly IStructureService structureService = structureService;
    readonly string fromName = fromName;
    readonly string toName = toName;

    public override void Execute(IModel model) => structureService.RenameNode(model, fromName, toName);

    public override void Revert(IModel model) => structureService.RenameNode(model, toName, fromName);
}
