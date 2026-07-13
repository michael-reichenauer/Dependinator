using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Removes a manual link; Revert re-adds it (and its visual line).
class DeleteLinkCommand(IStructureService structureService, string sourceName, string targetName) : Command
{
    readonly IStructureService structureService = structureService;
    readonly string sourceName = sourceName;
    readonly string targetName = targetName;

    public override void Execute(IModel model)
    {
        if (model.Links.TryGetValue(new LinkId(sourceName, targetName), out var link))
            model.RemoveLink(link);
    }

    public override void Revert(IModel model) => structureService.AddManualLink(model, sourceName, targetName);
}
