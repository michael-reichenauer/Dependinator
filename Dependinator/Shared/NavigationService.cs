using Dependinator.Diagrams;
using Dependinator.Models;
using DependinatorCore.Parsing;
using FileLocation = DependinatorCore.Parsing.FileLocation;

namespace Dependinator.Shared;

interface INavigationService
{
    Task ShowNodeAsync(NodeId nodeId);
    Task ShowNodeAsync(string fileLocation);
    Task ShowEditor(NodeId nodeId);
}

[Scoped]
class NavigationService(
    IApplicationEvents applicationEvents,
    IModelService modelService,
    IPanZoomService panZoomService,
    ISelectionService selectionService,
    IParserService parserService,
    IVsCodeSendService vsCodeSendService
) : INavigationService
{
    public async Task ShowNodeAsync(NodeId nodeId)
    {
        Log.Info("ShowNodeAsync for", nodeId);
        selectionService.Unselect();
        applicationEvents.TriggerUIStateChanged();
        await Task.Yield();

        Pos pos = Pos.None;
        double zoom = 0;
        if (
            !modelService.UseNodeN(
                nodeId,
                node =>
                {
                    (pos, zoom) = node.GetCenterPosAndZoom();
                }
            )
        )
            return;

        await panZoomService.PanZoomToAsync(pos, zoom);
        selectionService.Select(nodeId);
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task ShowNodeAsync(string fileLocation)
    {
        Log.Info("ShowNodeAsync for", fileLocation);
        var location = ParseFileLocation(fileLocation);
        string modelPath = null!;
        using (var model = modelService.UseModel())
        {
            modelPath = model.Path;
        }
        if (!Try(out var nodeName, out var e, await parserService.TryGetNodeAsync(modelPath, location)))
        {
            Log.Warn($"Failed to find node for {fileLocation}, {e.ErrorMessage}");
            return;
        }

        await ShowNodeAsync(NodeId.FromName(nodeName));
    }

    public async Task ShowEditor(NodeId nodeId)
    {
        Log.Info("ShowEditor for", nodeId);
        string modelPath = null!;
        var nodeName = "";
        using (var model = modelService.UseModel())
        {
            modelPath = model.Path;
            if (!model.TryGetNode(nodeId, out var node))
            {
                Log.Warn($"Failed find node for {nodeId}");
                return;
            }
            nodeName = node.Name;
        }

        if (!Try(out var fileLocation, out var e, await parserService.GetFileLocationAsync(modelPath, nodeName)))
        {
            Log.Warn($"Failed to find node for {fileLocation}, {e.ErrorMessage}");
            return;
        }

        await vsCodeSendService.ShowEditorAsync(fileLocation);
    }

    static FileLocation ParseFileLocation(string fileLocation)
    {
        var parts = fileLocation.Split('@');
        var path = parts[0];
        var line = parts.Length == 2 ? int.Parse(parts[1]) : 0;
        return new FileLocation(path, line);
    }
}
