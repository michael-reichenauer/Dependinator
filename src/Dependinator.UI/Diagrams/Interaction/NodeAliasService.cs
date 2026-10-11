using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using MudBlazor;

namespace Dependinator.UI.Diagrams.Interaction;

// Result returned by NodeAliasDialog: the (possibly edited) display name, or a request to remove it.
record NodeAliasDialogResult(string Alias, bool Clear);

// Edits a node's display name (the user's name shown instead of the one from the code) through a
// small dialog; the change is one undoable command. Reached from the node toolbar menu and the
// node's right-click menu. Not for annotations: a note has no name to alias, and a zone's label
// is its alias, edited in the zone dialog (ZoneService).
interface INodeAliasService
{
    Task EditAliasAsync(NodeId nodeId);
}

[Scoped]
class NodeAliasService(IModelMgr modelMgr, ICommandService commandService, IDialogService dialogService)
    : INodeAliasService
{
    public async Task EditAliasAsync(NodeId nodeId)
    {
        string current;
        string realName;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node) || NodeViewPolicy.IsAnnotation(node))
                return;
            current = node.Alias ?? "";
            realName = node.ShortName;
        }

        var parameters = new DialogParameters
        {
            { nameof(NodeAliasDialog.Alias), current.Length > 0 ? current : realName },
            { nameof(NodeAliasDialog.RealName), realName },
            { nameof(NodeAliasDialog.HasAlias), current.Length > 0 },
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
        };
        var dialog = await dialogService.ShowAsync<NodeAliasDialog>(null, parameters, options);
        var dialogResult = await dialog.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not NodeAliasDialogResult result)
            return;

        // The real name typed back, or nothing, means "no alias".
        var alias = result.Clear || result.Alias == realName ? "" : result.Alias;
        if (alias == current)
            return;

        commandService.Do(new NodeEditCommand(nodeId) { Alias = alias });
    }
}
