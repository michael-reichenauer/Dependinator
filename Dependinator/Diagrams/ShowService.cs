﻿using Dependinator.Models;

namespace Dependinator.Diagrams;


interface IShowService
{
    void Show(NodeId nodeId);
}

[Transient]
class ShowService(
    IModelService modelService,
    IPanZoomService panZoomService) : IShowService
{
    public void Show(NodeId nodeId)
    {
        Log.Info($"Show node {nodeId}");
        Pos pos = Pos.None;
        double zoom = 0;
        if (!modelService.UseNodeN(nodeId, node =>
        {
            (pos, zoom) = node.GetCenterPosAndZoom();
            Log.Info($"Show node {node.LongName} at {pos} zoom {zoom}");


        })) return;

        panZoomService.PanZoomTo(pos, zoom);

    }
}
