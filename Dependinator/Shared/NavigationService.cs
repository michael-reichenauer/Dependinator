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
    IScreenService screenService,
    IVsCodeSendService vsCodeSendService
) : INavigationService
{
    readonly SemaphoreSlim showNodeLock = new(1, 1);
    long showNodeRequestId = 0;

    public async Task ShowNodeAsync(NodeId nodeId)
    {
        var requestId = Interlocked.Increment(ref showNodeRequestId);
        await showNodeLock.WaitAsync();
        try
        {
            if (!IsLatestShowNodeRequest(requestId))
                return;

            Log.Info("ShowNodeAsync for", nodeId);
            selectionService.Unselect();
            applicationEvents.TriggerUIStateChanged();
            await Task.Yield();
            if (!IsLatestShowNodeRequest(requestId))
                return;

            if (!TryGetNodePosAndZoom(nodeId, out var pos, out var zoom))
                return;

            Log.Info($"Start Node Pos: {pos}, Zoom: {zoom}");
            if (!await panZoomService.PanZoomToAsync(pos, zoom))
                return;
            if (!TryGetNodePosAndZoom(nodeId, out pos, out zoom))
                return;
            Log.Info($"End Node Pos: {pos}, Zoom: {zoom}");

            if (!IsLatestShowNodeRequest(requestId))
                return;

            selectionService.Select(nodeId);
            applicationEvents.TriggerUIStateChanged();
            await LogShowNodeCenteringAsync(nodeId, pos, zoom, requestId);
        }
        finally
        {
            showNodeLock.Release();
        }
    }

    private bool TryGetNodePosAndZoom(NodeId nodeId, out Pos pos, out double zoom)
    {
        pos = Pos.None;
        zoom = 0.0;

        using (var model = modelService.UseModel())
        {
            if (!model.TryGetNode(nodeId, out var node))
                return false;

            if (node.EnsureLayoutForPath())
                model.ClearCachedSvg();

            (pos, zoom) = node.GetCenterPosAndZoom();
        }
        if (zoom <= 0)
            return false;

        return true;
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
            if (node.FileSpanOrParentSpan is null)
            {
                Log.Warn($"Failed find node file span {nodeId}");
                return;
            }
            fileSpan = node.FileSpanOrParentSpan;
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
                .Where(n => n.FileSpan!.Path.IsSameIc(fileLocation.Path))
                .OrderBy(n => n.FileSpan!.StarLine)
                .ToList();
        }

        if (!nodeCandidates.Any())
            return false;

        var currentNode = nodeCandidates.First();
        foreach (var node in nodeCandidates)
        {
            if (node.FileSpan!.StarLine == currentNode.FileSpan!.StarLine)
                continue;
            if (fileLocation.Line < node.FileSpan!.StarLine)
                break;
            currentNode = node;
        }
        Log.Info($"Found {currentNode.Name}, {currentNode.FileSpan}");
        nodeId = currentNode.Id;
        return true;
    }

    bool IsLatestShowNodeRequest(long requestId) => requestId == Volatile.Read(ref showNodeRequestId);

    async Task LogShowNodeCenteringAsync(NodeId nodeId, Pos targetPos, double targetZoom, long requestId)
    {
        if (!IsLatestShowNodeRequest(requestId))
            return;

        await Task.Yield(); // Allow one render cycle before reading DOM bounds.
        if (!IsLatestShowNodeRequest(requestId))
            return;

        var nodeElementId = PointerId.FromNode(nodeId).ElementId;
        if (!Try(out var nodeRect, out var _, await screenService.GetBoundingRectangle(nodeElementId)))
        {
            Log.Debug($"ShowNode Centering: node bounds unavailable for {nodeId} ({nodeElementId})");
            return;
        }
        if (!Try(out var svgRect, out var _, await screenService.GetBoundingRectangle("svgcanvas")))
        {
            Log.Debug($"ShowNode Centering: canvas bounds unavailable for {nodeId}");
            return;
        }

        var nodeCenterX = nodeRect.X + nodeRect.Width / 2;
        var nodeCenterY = nodeRect.Y + nodeRect.Height / 2;
        var svgCenterX = svgRect.X + svgRect.Width / 2;
        var svgCenterY = svgRect.Y + svgRect.Height / 2;
        var dxPixels = nodeCenterX - svgCenterX;
        var dyPixels = nodeCenterY - svgCenterY;
        var distPixels = Math.Sqrt(dxPixels * dxPixels + dyPixels * dyPixels);

        var currentOffset = modelService.Offset;
        var currentZoom = modelService.Zoom;
        var modelCenterPos = new Pos(
            currentOffset.X + screenService.SvgRect.Width / 2 * currentZoom,
            currentOffset.Y + screenService.SvgRect.Height / 2 * currentZoom
        );
        var modelDx = modelCenterPos.X - targetPos.X;
        var modelDy = modelCenterPos.Y - targetPos.Y;

        Log.Debug(
            $"ShowNode Centering: node={nodeId} pxDelta=({dxPixels:0.##},{dyPixels:0.##}) pxDist={distPixels:0.##} "
                + $"modelDelta=({modelDx:0.##},{modelDy:0.##}) targetZoom={targetZoom:0.####} zoom={currentZoom:0.####}"
        );
    }
}
