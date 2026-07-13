using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Adds a manually created link (and its visual line) between two existing nodes. Revert removes it.
class AddLinkCommand(IStructureService structureService, string sourceName, string targetName) : Command
{
    readonly IStructureService structureService = structureService;
    readonly string sourceName = sourceName;
    readonly string targetName = targetName;

    public override void Execute(IModel model) => structureService.AddManualLink(model, sourceName, targetName);

    public override void Revert(IModel model)
    {
        if (model.Links.TryGetValue(new LinkId(sourceName, targetName), out var link))
            model.RemoveLink(link);
    }
}
