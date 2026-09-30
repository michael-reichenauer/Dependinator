using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams;

readonly record struct ViewContextItem(NodeId Id, string Name, string LongName);

// Where the user is in the diagram, for wayfinding (the breadcrumb, Escape to zoom out). A map
// app keeps a place name on screen at every zoom; here a container's name sits at its corner
// and is off-screen once the user has zoomed into it, so the chain of containers around the
// viewport center is computed instead: the "you are here".
interface IViewContextService
{
    // The containers from the top level down to the innermost open container under the
    // viewport center, top first. Empty at the overview, where the center is not inside any
    // open container.
    IReadOnlyList<ViewContextItem> GetViewCenterChain();

    // The selected node's chain from the top level down to the node itself; empty if no node
    // is selected.
    IReadOnlyList<ViewContextItem> GetSelectedChain();
}

[Scoped]
class ViewContextService(
    IModelMgr modelMgr,
    IScreenService screenService,
    IViewOptions viewOptions,
    Interaction.ISelectionService selectionService
) : IViewContextService
{
    public IReadOnlyList<ViewContextItem> GetViewCenterChain()
    {
        var svgRect = screenService.SvgRect;
        if (svgRect.Width <= 0 || svgRect.Height <= 0)
            return [];

        using var model = modelMgr.UseModel();
        if (model.Zoom <= 0 || model.Offset == Pos.None)
            return [];

        var center = new Pos(
            model.Offset.X + svgRect.Width / 2 * model.Zoom,
            model.Offset.Y + svgRect.Height / 2 * model.Zoom
        );

        List<ViewContextItem> chain = [];
        var node = model.Root;
        while (FindOpenChildAt(node, center, model.Zoom) is { } child)
        {
            if (IsShownInChain(child))
                chain.Add(ToItem(child));
            node = child;
        }

        return chain;
    }

    // The child whose box contains the point and whose inside is visible at this zoom (an icon
    // is a closed box: the user is next to it, not inside it).
    Node? FindOpenChildAt(Node node, Pos point, double modelZoom)
    {
        foreach (var child in node.Children)
        {
            if (child.IsNote || (child.IsHidden && !viewOptions.ShowHiddenNodes))
                continue;
            if (!NodeViewPolicy.IsChildrenShown(child, modelZoom))
                continue;

            var (pos, zoom) = child.GetPosAndZoom();
            var width = child.Boundary.Width * zoom;
            var height = child.Boundary.Height * zoom;
            if (point.X >= pos.X && point.X <= pos.X + width && point.Y >= pos.Y && point.Y <= pos.Y + height)
                return child;
        }

        return null;
    }

    public IReadOnlyList<ViewContextItem> GetSelectedChain()
    {
        if (!selectionService.SelectedId.IsNode)
            return [];

        using var model = modelMgr.UseModel();
        if (!model.Nodes.TryGetValue(selectionService.SelectedId.NodeId, out var node) || node.IsNote)
            return [];

        return node.AncestorsAndSelf().Where(IsShownInChain).Reverse().Select(ToItem).ToList();
    }

    // The root is invisible, a pass-through container is drawn as its parent, and the solution
    // node repeats the model's own name (the breadcrumb starts with the model).
    static bool IsShownInChain(Node node) =>
        !node.IsRoot && !node.IsPassThrough && node.Type != Dependinator.Core.Parsing.NodeType.Solution;

    static ViewContextItem ToItem(Node node) => new(node.Id, node.ShortName, node.LongName);
}
