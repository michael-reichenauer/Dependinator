using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams;

// Orchestrates the "manual design" interactions: adding user-drawn nodes (double-click empty
// canvas → inline name prompt) and user-drawn links (select source → add-link mode → click
// target). All mutations go through the undoable CommandService.
interface IManualEditService
{
    // Add-node inline prompt state (the Canvas renders a name input when IsAddingNode is true).
    bool IsAddingNode { get; }
    Pos AddNodeScreenPos { get; }
    event Action? StateChanged;

    // Begins adding a node at a double-clicked canvas position; shows the inline name prompt.
    void BeginAddNode(PointerEvent e);

    // Creates the node with the given name; returns false if the name is empty or already used.
    bool CommitAddNode(string name);
    void CancelAddNode();

    // Add-link mode.
    bool IsAddingLink { get; }
    void BeginAddLink(NodeId sourceId);

    // Completes a pending add-link with the clicked target. Returns true if a link was created.
    bool TryCompleteAddLink(PointerId targetPointerId);
    void CancelAddLink();

    // Deletes a leaf manual node together with its manual links (undoable).
    void DeleteManualNode(NodeId nodeId);
}

[Scoped]
class ManualEditService(IModelMgr modelMgr, ICommandService commandService, IStructureService structureService)
    : IManualEditService
{
    const double DefaultWidth = 80;
    const double DefaultHeight = 40;

    // Pending add-node placement (set between BeginAddNode and CommitAddNode/CancelAddNode).
    string pendingParentName = "";
    Rect pendingBoundary = Rect.None;

    public bool IsAddingNode { get; private set; }
    public Pos AddNodeScreenPos { get; private set; } = Pos.None;
    public bool IsAddingLink { get; private set; }
    string addLinkSourceName = "";

    public event Action? StateChanged;

    public void BeginAddNode(PointerEvent e)
    {
        // Any pending link-drawing is superseded by starting a node add.
        IsAddingLink = false;

        using (var model = modelMgr.UseModel())
        {
            var container = ResolveContainer(model, PointerId.Parse(e.TargetId));

            // Screen → canvas (root) coordinates, then into the container's inner child space.
            var svgX = e.OffsetX * model.Zoom + model.Offset.X;
            var svgY = e.OffsetY * model.Zoom + model.Offset.Y;

            var (parentPos, parentZoom) = container.GetPosAndZoom();
            var zoom = container.ContainerZoom * parentZoom;
            var localX = (svgX - parentPos.X - container.ContainerOffset.X * parentZoom) / zoom;
            var localY = (svgY - parentPos.Y - container.ContainerOffset.Y * parentZoom) / zoom;

            pendingParentName = container.Name;
            pendingBoundary = new Rect(
                SnapToGrid(localX - DefaultWidth / 2),
                SnapToGrid(localY - DefaultHeight / 2),
                DefaultWidth,
                DefaultHeight
            );
        }

        AddNodeScreenPos = new Pos(e.ClientX, e.ClientY);
        IsAddingNode = true;
        StateChanged?.Invoke();
    }

    public bool CommitAddNode(string name)
    {
        var trimmed = name?.Trim() ?? "";
        if (!IsAddingNode || trimmed.Length == 0)
            return false;

        using (var model = modelMgr.UseModel())
        {
            if (model.Nodes.ContainsKey(NodeId.FromName(trimmed)))
                return false; // Name is the node identity; reject duplicates.
        }

        commandService.Do(new AddNodeCommand(trimmed, pendingParentName, pendingBoundary));
        ResetAddNode();
        return true;
    }

    public void CancelAddNode()
    {
        if (!IsAddingNode)
            return;
        ResetAddNode();
    }

    public void BeginAddLink(NodeId sourceId)
    {
        addLinkSourceName = ResolveNodeName(sourceId);
        if (addLinkSourceName.Length == 0)
            return;
        IsAddingLink = true;
        StateChanged?.Invoke();
    }

    public bool TryCompleteAddLink(PointerId targetPointerId)
    {
        if (!IsAddingLink || !targetPointerId.IsNode)
            return false;

        var targetName = ResolveNodeName(targetPointerId.NodeId);
        IsAddingLink = false;
        StateChanged?.Invoke();

        if (targetName.Length == 0 || targetName == addLinkSourceName)
            return false;

        commandService.Do(new AddLinkCommand(structureService, addLinkSourceName, targetName));
        return true;
    }

    public void CancelAddLink()
    {
        if (!IsAddingLink)
            return;
        IsAddingLink = false;
        StateChanged?.Invoke();
    }

    public void DeleteManualNode(NodeId nodeId)
    {
        var commands = new List<Command>();
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node) || !node.IsManual || node.Children.Count > 0)
                return;

            // Remove attached manual links first (composed so undo restores them with the node).
            var attached = node.SourceLinks.Concat(node.TargetLinks).Where(l => l.IsManual).Distinct();
            foreach (var link in attached)
                commands.Add(new DeleteLinkCommand(structureService, link.Source.Name, link.Target.Name));
        }

        commands.Add(new DeleteNodeCommand(nodeId));
        commandService.Do(commands.Count == 1 ? commands[0] : new CompositeCommand([.. commands]));
    }

    void ResetAddNode()
    {
        IsAddingNode = false;
        AddNodeScreenPos = Pos.None;
        pendingParentName = "";
        pendingBoundary = Rect.None;
        StateChanged?.Invoke();
    }

    // The container the new node is added into: the double-clicked container node, else the
    // clicked node's parent, else the root.
    static Node ResolveContainer(IModel model, PointerId pointerId)
    {
        if (pointerId.IsNode && model.Nodes.TryGetValue(pointerId.NodeId, out var node) && !node.IsRoot)
        {
            if (IsContainerView(node, model.Zoom))
                return node;
            if (node.Parent is { } parent)
                return parent;
        }
        return model.Root;
    }

    // Mirrors InteractionService.IsContainer: the node is shown as an expanded box (not an icon).
    static bool IsContainerView(Node node, double modelZoom)
    {
        var nodeZoom = 1 / (node.GetZoom() * modelZoom);
        return !NodeSvg.IsToLargeToBeSeen(nodeZoom) && !NodeSvg.IsShowIcon(node.Type, nodeZoom);
    }

    string ResolveNodeName(NodeId nodeId)
    {
        using var model = modelMgr.UseModel();
        return model.Nodes.TryGetValue(nodeId, out var node) ? node.Name : "";
    }

    static double SnapToGrid(double value) => Math.Round(value / NodeGrid.SnapSize) * NodeGrid.SnapSize;
}
