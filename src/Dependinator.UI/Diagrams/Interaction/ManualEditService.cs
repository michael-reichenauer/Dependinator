using Dependinator.UI.Diagrams.Icons;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using MudBlazor;

namespace Dependinator.UI.Diagrams.Interaction;

// Orchestrates the "manual design" interactions: adding user-drawn nodes (double-click empty
// canvas → icon selector dialog), renaming them in place, and drawing user-drawn links (drag a
// hovered node's link handle onto the target). All mutations go through the undoable
// CommandService.
interface IManualEditService
{
    // Inline name-entry state (the Canvas renders a name input while IsNameEntryOpen is true),
    // used by the rename-node flow.
    bool IsNameEntryOpen { get; }
    Pos NameEntryScreenPos { get; }
    string NameEntryInitialValue { get; }
    string NameEntryLabel { get; }
    event Action? StateChanged;

    // Adds a node at a double-clicked (or clicked, in place mode) canvas position: shows the icon
    // selector dialog, then creates the node with the chosen icon, named after that icon.
    Task AddNodeAtAsync(PointerEvent e);

    // "Add node" placement mode: armed from the app menu, the next canvas click adds a node at that
    // position (parallels INoteService.BeginPlaceNote).
    bool IsPlacingNode { get; }
    void BeginPlaceNode();
    void CancelPlaceNode();

    // Begins renaming an existing node, anchoring the inline prompt at the given screen position.
    void BeginRenameNode(NodeId nodeId, Pos screenPos);

    // Commits the inline name entry (renames); false if the name is empty or already used.
    bool CommitNameEntry(string name);
    void CancelNameEntry();

    // Drag-to-link: pressing an icon node's link handle and dragging draws a dotted preview line
    // (the Canvas renders it while IsLinkDragActive); dropping on an icon node creates a manual
    // link, dropping on the canvas or a container adds a new node to link to. Positions are in
    // viewport (client) pixels, matching the fixed preview overlay.
    bool IsLinkDragActive { get; }
    Pos LinkDragStart { get; }
    Pos LinkDragEnd { get; }
    void BeginLinkDrag(NodeId sourceId, Pos startPos);
    void UpdateLinkDrag(Pos pos);

    // Completes a link drag with the dropped-on target; always resets the drag state. Dropping
    // on an icon node links to it; dropping on the canvas background or an expanded container
    // adds a new node there (via the icon selector dialog) and links to that node instead.
    // Other drops (self, outside the diagram, canceled dialog) create nothing. canvasPos is
    // the drop point in canvas (svg-local) pixels, null if unknown.
    Task CompleteLinkDragAsync(PointerId targetPointerId, Pos? canvasPos);
    void CancelLinkDrag();

    // Deletes a leaf manual node together with its manual links (undoable).
    void DeleteManualNode(NodeId nodeId);

    // Deletes the manual link(s) a line represents (undoable); no-op if the line has parsed links.
    void DeleteManualLine(LineId lineId);
}

[Scoped]
class ManualEditService(
    IModelMgr modelMgr,
    ICommandService commandService,
    IStructureService structureService,
    ISelectionService selectionService,
    IDialogService dialogService
) : IManualEditService
{
    // Match the size parsed nodes get from the auto-layout.
    static readonly double DefaultWidth = NodeLayout.DefaultSize.Width;
    static readonly double DefaultHeight = NodeLayout.DefaultSize.Height;

    // The node being renamed (set between BeginRenameNode and commit/cancel): its current full
    // name and its parent's full name (used to re-qualify the new name).
    string renameFromName = "";
    string renameParentName = "";

    public bool IsNameEntryOpen { get; private set; }
    public Pos NameEntryScreenPos { get; private set; } = Pos.None;
    public string NameEntryInitialValue { get; private set; } = "";
    public string NameEntryLabel => "Rename node";

    public bool IsLinkDragActive { get; private set; }
    public Pos LinkDragStart { get; private set; } = Pos.None;
    public Pos LinkDragEnd { get; private set; } = Pos.None;
    string dragLinkSourceName = "";

    public bool IsPlacingNode { get; private set; }

    public event Action? StateChanged;

    public void BeginPlaceNode()
    {
        IsPlacingNode = true;
        StateChanged?.Invoke();
    }

    public void CancelPlaceNode()
    {
        if (!IsPlacingNode)
            return;
        IsPlacingNode = false;
        StateChanged?.Invoke();
    }

    public async Task AddNodeAtAsync(PointerEvent e)
    {
        // Any armed placement is superseded by starting a node add.
        IsPlacingNode = false;
        StateChanged?.Invoke();

        string parentName;
        Rect boundary;
        using (var model = modelMgr.UseModel())
        {
            var container = DiagramPlacement.ResolveContainer(model, PointerId.Parse(e.TargetId));
            var local = DiagramPlacement.ToContainerLocal(model, container, e);

            parentName = container.Name;
            boundary = new Rect(
                NodeGrid.Snap(local.X - DefaultWidth / 2),
                NodeGrid.Snap(local.Y - DefaultHeight / 2),
                DefaultWidth,
                DefaultHeight
            );
        }

        var iconName = await ShowIconSelectorAsync();
        if (iconName is null)
            return;

        // The node is named after the chosen icon; a numeric suffix keeps it unique under the
        // parent. The user can rename it afterwards via the node menu.
        string fullName;
        using (var model = modelMgr.UseModel())
        {
            var shortName = UniqueShortName(model, parentName, IconLibrary.ToDisplayName(iconName));
            fullName = DiagramPlacement.ComposeFullName(parentName, shortName);
        }

        commandService.Do(new AddNodeCommand(fullName, parentName, boundary, iconName: iconName));
    }

    // Shows the icon selector in picker mode; returns the chosen icon name, or null if canceled.
    async Task<string?> ShowIconSelectorAsync()
    {
        var parameters = new DialogParameters
        {
            { nameof(IconSelectorDialog.SelectOnly), true },
            { nameof(IconSelectorDialog.CurrentIconName), "Module" },
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            NoHeader = true,
            Position = DialogPosition.TopCenter,
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
        };

        var dialog = await dialogService.ShowAsync<IconSelectorDialog>(null, parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled)
            return null;
        return result.Data as string;
    }

    // The first of "Name", "Name 2", "Name 3", … not already used under the parent (full name is
    // the node identity, so the same short name can still be used under other parents).
    static string UniqueShortName(IModel model, string parentName, string baseName)
    {
        for (var i = 1; ; i++)
        {
            var candidate = i == 1 ? baseName : $"{baseName} {i}";
            var fullName = DiagramPlacement.ComposeFullName(parentName, candidate);
            if (!model.Nodes.ContainsKey(NodeId.FromName(fullName)))
                return candidate;
        }
    }

    public void BeginRenameNode(NodeId nodeId, Pos screenPos)
    {
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node))
                return;

            renameFromName = node.Name;
            renameParentName = node.Parent?.Name ?? "";
            // The text box shows and edits the short name; the parent prefix is kept.
            NameEntryInitialValue = node.ShortName;
        }

        IsNameEntryOpen = true;
        NameEntryScreenPos = screenPos;
        StateChanged?.Invoke();
    }

    public bool CommitNameEntry(string name)
    {
        var trimmed = name?.Trim() ?? "";
        if (!IsNameEntryOpen || trimmed.Length == 0)
            return false;

        // The typed text is the short name; the node's identity is qualified by its parent (like
        // parsed nodes), so the same short name can be used under different parents.
        var fullName = DiagramPlacement.ComposeFullName(renameParentName, trimmed);

        // Renaming to the unchanged name is a no-op, but a valid "commit" that closes the prompt.
        if (fullName == renameFromName)
        {
            ResetNameEntry();
            return true;
        }

        using (var model = modelMgr.UseModel())
        {
            if (model.Nodes.ContainsKey(NodeId.FromName(fullName)))
                return false; // Full name is the node identity; reject duplicates.
        }

        commandService.Do(new RenameNodeCommand(structureService, renameFromName, fullName));
        ResetNameEntry();

        // A rename replaces the node, so the previous selection (old id) is now stale; move the
        // selection to the resulting node.
        selectionService.Unselect();
        selectionService.Select(NodeId.FromName(fullName)).RunInBackground();
        return true;
    }

    public void CancelNameEntry()
    {
        if (!IsNameEntryOpen)
            return;
        ResetNameEntry();
    }

    public void BeginLinkDrag(NodeId sourceId, Pos startPos)
    {
        dragLinkSourceName = ResolveNodeName(sourceId);
        if (dragLinkSourceName.Length == 0)
            return;
        IsLinkDragActive = true;
        LinkDragStart = startPos;
        LinkDragEnd = startPos;
        StateChanged?.Invoke();
    }

    // No StateChanged here: the canvas already re-renders on every pointer event, and other
    // subscribers (e.g. toolbars) do not need per-move notifications.
    public void UpdateLinkDrag(Pos pos)
    {
        if (!IsLinkDragActive)
            return;
        LinkDragEnd = pos;
    }

    public async Task CompleteLinkDragAsync(PointerId targetPointerId, Pos? canvasPos)
    {
        if (!IsLinkDragActive)
            return;

        var sourceName = dragLinkSourceName;
        ResetLinkDrag();

        // Icon-view node drops link to the existing node; canvas/container drops add a new node
        // at the drop point (in the resolved container) and link to it.
        var linkTargetName = "";
        var parentName = "";
        var boundary = Rect.None;
        using (var model = modelMgr.UseModel())
        {
            if (targetPointerId.IsNode)
            {
                if (!model.Nodes.TryGetValue(targetPointerId.NodeId, out var node))
                    return;
                if (!node.IsRoot && !NodeViewPolicy.IsContainerView(node, model.Zoom))
                    linkTargetName = node.Name;
            }
            else if (!targetPointerId.IsCanvas)
            {
                return; // Dropped outside the diagram (or on a line/handle): nothing to do.
            }

            if (linkTargetName.Length == 0)
            {
                if (canvasPos is not { } pos)
                    return;

                var container = DiagramPlacement.ResolveContainer(model, targetPointerId);
                var local = DiagramPlacement.ToContainerLocal(model, container, pos);
                parentName = container.Name;
                boundary = new Rect(
                    NodeGrid.Snap(local.X - DefaultWidth / 2),
                    NodeGrid.Snap(local.Y - DefaultHeight / 2),
                    DefaultWidth,
                    DefaultHeight
                );
            }
        }

        if (linkTargetName.Length > 0)
        {
            if (linkTargetName != sourceName)
                commandService.Do(new AddLinkCommand(structureService, sourceName, linkTargetName));
            return;
        }

        var iconName = await ShowIconSelectorAsync();
        if (iconName is null)
            return;

        string fullName;
        using (var model = modelMgr.UseModel())
        {
            var shortName = UniqueShortName(model, parentName, IconLibrary.ToDisplayName(iconName));
            fullName = DiagramPlacement.ComposeFullName(parentName, shortName);
        }

        // One undoable step: undoing removes both the link and the node it was drawn to.
        commandService.Do(
            new CompositeCommand(
                new AddNodeCommand(fullName, parentName, boundary, iconName: iconName),
                new AddLinkCommand(structureService, sourceName, fullName)
            )
        );
    }

    public void CancelLinkDrag()
    {
        if (!IsLinkDragActive)
            return;
        ResetLinkDrag();
    }

    void ResetLinkDrag()
    {
        IsLinkDragActive = false;
        LinkDragStart = Pos.None;
        LinkDragEnd = Pos.None;
        dragLinkSourceName = "";
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

    public void DeleteManualLine(LineId lineId)
    {
        var commands = new List<Command>();
        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(lineId, out var line))
                return;
            if (!line.Links.Any() || line.Links.Any(l => !l.IsManual))
                return;

            foreach (var link in line.Links)
            {
                commands.Add(new DeleteLinkCommand(structureService, link.Source.Name, link.Target.Name));
            }
        }

        commandService.Do(commands.Count == 1 ? commands[0] : new CompositeCommand([.. commands]));
    }

    void ResetNameEntry()
    {
        IsNameEntryOpen = false;
        NameEntryScreenPos = Pos.None;
        NameEntryInitialValue = "";
        renameFromName = "";
        renameParentName = "";
        StateChanged?.Invoke();
    }

    string ResolveNodeName(NodeId nodeId)
    {
        using var model = modelMgr.UseModel();
        return model.Nodes.TryGetValue(nodeId, out var node) ? node.Name : "";
    }
}
