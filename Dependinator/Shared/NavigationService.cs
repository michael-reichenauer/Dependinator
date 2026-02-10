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

        if (!TryGetNodeIdForFileLocation(location, out var nodeId))
        {
            Log.Warn($"Failed to find node for {fileLocation}");
            return;
        }
        Log.Info("Found node", nodeId);
        await ShowNodeAsync(nodeId);
    }

    public async Task ShowEditor(NodeId nodeId)
    {
        Log.Info("ShowEditor for", nodeId);

        FileSpan? fileSpan;
        using (var model = modelService.UseModel())
        {
            if (!model.TryGetNode(nodeId, out var node))
            {
                Log.Warn($"Failed find node for {nodeId}");
                return;
            }
            if (node.FileSpan is null)
            {
                Log.Warn($"Failed find node file span {nodeId}");
                return;
            }
            fileSpan = node.FileSpan;
        }
        Log.Info("Show editor for", fileSpan.Path, fileSpan.StarLine);

        await vsCodeSendService.ShowEditorAsync(new FileLocation(fileSpan.Path, fileSpan.StarLine + 1));
    }

    static FileLocation ParseFileLocation(string fileLocation)
    {
        var parts = fileLocation.Split('@');
        var path = parts[0];
        var line = Math.Max(0, (parts.Length == 2 ? int.Parse(parts[1]) : 0) - 1);
        return new FileLocation(path, line);
    }

    bool TryGetNodeIdForFileLocation(FileLocation fileLocation, out NodeId nodeId)
    {
        nodeId = null!;
        List<Models.Node> nodeCandidates = [];
        using (var model = modelService.UseModel())
        {
            nodeCandidates = model
                .Items.Values.OfType<Models.Node>()
                .Where(n => n.FileSpan is not null)
                .Where(n => n.FileSpan!.Path.StartsWithIc(fileLocation.Path))
                .OrderBy(n => n.FileSpan!.StarLine)
                .ToList();
        }

        if (!nodeCandidates.Any())
            return false;

        var currentNode = nodeCandidates.First();
        foreach (var node in nodeCandidates)
        {
            if (fileLocation.Line < node.FileSpan!.StarLine)
                break;
            currentNode = node;
        }

        nodeId = currentNode.Id;
        return true;
    }
}
