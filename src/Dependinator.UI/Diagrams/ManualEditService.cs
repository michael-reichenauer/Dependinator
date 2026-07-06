using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams;

// Orchestrates the "manual design" interactions: adding user-drawn nodes (double-click empty
// canvas → inline name prompt), renaming them in place, and drawing user-drawn links (select
// source → add-link mode → click target). All mutations go through the undoable CommandService.
interface IManualEditService
{
    // Inline name-entry state (the Canvas renders a name input while IsNameEntryOpen is true),
    // shared by the add-node and rename-node flows.
    bool IsNameEntryOpen { get; }
    Pos NameEntryScreenPos { get; }
    string NameEntryInitialValue { get; }
    string NameEntryLabel { get; }
    event Action? StateChanged;

    // Begins adding a node at a double-clicked canvas position; shows the inline name prompt.
    void BeginAddNode(PointerEvent e);

    // Begins renaming an existing node, anchoring the inline prompt at the given screen position.
    void BeginRenameNode(NodeId nodeId, Pos screenPos);

    // Commits the inline name entry (adds or renames); false if the name is empty or already used.
    bool CommitNameEntry(string name);
    void CancelNameEntry();

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
class ManualEditService(
    IModelMgr modelMgr,
    ICommandService commandService,
    IStructureService structureService,
    ISelectionService selectionService
) : IManualEditService
{
    // Match the size parsed nodes get from the auto-layout.
    static readonly double DefaultWidth = NodeLayout.DefaultSize.Width;
    static readonly double DefaultHeight = NodeLayout.DefaultSize.Height;

    enum EntryMode
    {
        None,
        Add,
        Rename,
    }

    EntryMode entryMode = EntryMode.None;

    // Pending add-node placement (set between BeginAddNode and commit/cancel).
    string pendingParentName = "";
    Rect pendingBoundary = Rect.None;

    // The node being renamed (set between BeginRenameNode and commit/cancel): its current full
    // name and its parent's full name (used to re-qualify the new name).
    string renameFromName = "";
    string renameParentName = "";

    public bool IsNameEntryOpen => entryMode != EntryMode.None;
    public Pos NameEntryScreenPos { get; private set; } = Pos.None;
    public string NameEntryInitialValue { get; private set; } = "";
    public string NameEntryLabel => entryMode == EntryMode.Rename ? "Rename node" : "New node name";

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

        entryMode = EntryMode.Add;
        NameEntryInitialValue = "";
        NameEntryScreenPos = new Pos(e.ClientX, e.ClientY);
        StateChanged?.Invoke();
    }

    public void BeginRenameNode(NodeId nodeId, Pos screenPos)
    {
        IsAddingLink = false;

        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node))
                return;

            renameFromName = node.Name;
            renameParentName = node.Parent?.Name ?? "";
            // The text box shows and edits the short name; the parent prefix is kept.
            NameEntryInitialValue = node.ShortName;
        }

        entryMode = EntryMode.Rename;
        NameEntryScreenPos = screenPos;
        StateChanged?.Invoke();
    }

    public bool CommitNameEntry(string name)
    {
        var trimmed = name?.Trim() ?? "";
        if (!IsNameEntryOpen || trimmed.Length == 0)
            return false;

        var isRename = entryMode == EntryMode.Rename;
        var parentName = isRename ? renameParentName : pendingParentName;

        // The typed text is the short name; the node's identity is qualified by its parent (like
        // parsed nodes), so the same short name can be used under different parents.
        var fullName = ComposeFullName(parentName, trimmed);

        // Renaming to the unchanged name is a no-op, but a valid "commit" that closes the prompt.
        if (isRename && fullName == renameFromName)
        {
            ResetNameEntry();
            return true;
        }

        using (var model = modelMgr.UseModel())
        {
            if (model.Nodes.ContainsKey(NodeId.FromName(fullName)))
                return false; // Full name is the node identity; reject duplicates.
        }

        Command command = isRename
            ? new RenameNodeCommand(structureService, renameFromName, fullName)
            : new AddNodeCommand(fullName, pendingParentName, pendingBoundary);
        commandService.Do(command);
        ResetNameEntry();

        // A rename replaces the node, so the previous selection (old id) is now stale; move the
        // selection to the resulting node.
        if (isRename)
        {
            selectionService.Unselect();
            selectionService.Select(NodeId.FromName(fullName));
        }
        return true;
    }

    // The identity name of a manual node: the parent's full name and the typed short name joined by
    // a dot (mirroring parsed node names). Top-level nodes (empty parent name) keep just the short.
    static string ComposeFullName(string parentName, string shortName) =>
        string.IsNullOrEmpty(parentName) ? shortName : $"{parentName}.{shortName}";

    public void CancelNameEntry()
    {
        if (!IsNameEntryOpen)
            return;
        ResetNameEntry();
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

    // Deletes a manual node and its whole subtree (children and their links), as one undoable step.
    public void DeleteManualNode(NodeId nodeId)
    {
        var commands = new List<Command>();
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node) || !node.IsManual)
                return;

            // Post-order: delete descendants (and their links) before their parents, so undo — which
            // reverts in reverse order — restores each parent before its children.
            var seenLinks = new HashSet<LinkId>();
            foreach (var descendant in node.DescendantsAndSelfPostOrder().ToList())
            {
                var links = descendant.SourceLinks.Concat(descendant.TargetLinks).Where(l => l.IsManual);
                foreach (var link in links)
                {
                    if (seenLinks.Add(link.Id))
                        commands.Add(new DeleteLinkCommand(structureService, link.Source.Name, link.Target.Name));
                }
                commands.Add(new DeleteNodeCommand(descendant.Id));
            }
        }

        if (commands.Count == 0)
            return;
        commandService.Do(commands.Count == 1 ? commands[0] : new CompositeCommand([.. commands]));
    }

    void ResetNameEntry()
    {
        entryMode = EntryMode.None;
        NameEntryScreenPos = Pos.None;
        NameEntryInitialValue = "";
        pendingParentName = "";
        pendingBoundary = Rect.None;
        renameFromName = "";
        renameParentName = "";
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
