using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using MudBlazor;

namespace Dependinator.UI.Diagrams;

// Result returned by NoteDialog: the (possibly edited) id and description, or a request to delete.
record NoteDialogResult(string Id, string Description, bool Delete);

// Orchestrates note annotations: arming "place note" mode from the menu, placing a note at the
// clicked canvas position (with the next free id pre-filled), and editing/deleting an existing
// note. A note is a manual Node (IsNote) parented into whatever container it is dropped in (like
// manual-node add), so it scales and hides with that container. All mutations go through the
// undoable CommandService.
interface INoteService
{
    // True while the user has chosen "Add note" and the next canvas click places the note.
    bool IsPlacingNote { get; }
    event Action? StateChanged;

    void BeginPlaceNote();
    void CancelPlaceNote();

    // Places a note at the clicked canvas position (opens the note dialog). Called by the
    // interaction service on a click while IsPlacingNote is true.
    Task PlaceNoteAtAsync(PointerEvent e);

    // Opens the note dialog to edit (or delete) an existing note.
    Task EditNoteAsync(NodeId nodeId);

    bool IsNoteNode(NodeId nodeId);
}

[Scoped]
class NoteService(
    IModelMgr modelMgr,
    ICommandService commandService,
    IStructureService structureService,
    ISelectionService selectionService,
    IDialogService dialogService
) : INoteService
{
    // The note circle's bounding box in root child coordinates (a couple of grid cells).
    const double NoteSize = 40;

    public bool IsPlacingNote { get; private set; }
    public event Action? StateChanged;

    public void BeginPlaceNote()
    {
        IsPlacingNote = true;
        StateChanged?.Invoke();
    }

    public void CancelPlaceNote()
    {
        if (!IsPlacingNote)
            return;
        IsPlacingNote = false;
        StateChanged?.Invoke();
    }

    public bool IsNoteNode(NodeId nodeId)
    {
        using var model = modelMgr.UseModel();
        return model.Nodes.TryGetValue(nodeId, out var node) && node.IsNote;
    }

    public async Task PlaceNoteAtAsync(PointerEvent e)
    {
        IsPlacingNote = false;
        StateChanged?.Invoke();

        Rect boundary;
        string parentName;
        string nextId;
        using (var model = modelMgr.UseModel())
        {
            // Resolve the parent the same way manual-node add does: the clicked container (when
            // shown expanded), else the clicked node's parent, else the root. This keeps the note
            // sized relative to and scaling/hiding with the container it is dropped into.
            var container = ResolveContainer(model, PointerId.Parse(e.TargetId));

            // Screen → canvas (root) coordinates, then into the container's inner child space.
            var svgX = e.OffsetX * model.Zoom + model.Offset.X;
            var svgY = e.OffsetY * model.Zoom + model.Offset.Y;

            var (parentPos, parentZoom) = container.GetPosAndZoom();
            var zoom = container.ContainerZoom * parentZoom;
            var localX = (svgX - parentPos.X - container.ContainerOffset.X * parentZoom) / zoom;
            var localY = (svgY - parentPos.Y - container.ContainerOffset.Y * parentZoom) / zoom;

            boundary = new Rect(
                SnapToGrid(localX - NoteSize / 2),
                SnapToGrid(localY - NoteSize / 2),
                NoteSize,
                NoteSize
            );
            parentName = container.Name;
            nextId = NextNoteId(model);
        }

        var result = await ShowDialogAsync(nextId, "", isEdit: false);
        if (result is null || result.Delete)
            return;

        // The typed id is the note's short name; its identity is qualified by its parent (like
        // parsed and manual nodes), so the same id can be reused under different parents.
        var name = ComposeFullName(parentName, result.Id);
        using (var model = modelMgr.UseModel())
        {
            if (model.Nodes.ContainsKey(NodeId.FromName(name)))
                return; // Id already used under this parent; the model is the source of truth.
        }

        commandService.Do(
            new AddNodeCommand(name, parentName, boundary, isNote: true, description: result.Description)
        );
        selectionService.Select(NodeId.FromName(name));
    }

    public async Task EditNoteAsync(NodeId nodeId)
    {
        string oldShortId;
        string oldFullName;
        string parentName;
        string oldDescription;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node) || !node.IsNote)
                return;
            oldShortId = node.ShortName;
            oldFullName = node.Name;
            parentName = node.Parent?.Name ?? "";
            oldDescription = node.Description ?? "";
        }

        var result = await ShowDialogAsync(oldShortId, oldDescription, isEdit: true);
        if (result is null)
            return;

        if (result.Delete)
        {
            selectionService.Unselect();
            commandService.Do(new DeleteNodeCommand(nodeId));
            return;
        }

        // Re-qualify the typed id under the note's existing parent to get its new identity.
        var newFullName = ComposeFullName(parentName, result.Id);
        var isRename = newFullName != oldFullName;
        if (isRename)
        {
            using var model = modelMgr.UseModel();
            if (model.Nodes.ContainsKey(NodeId.FromName(newFullName)))
                return; // New id already used under this parent.
        }

        var commands = new List<Command>();
        if (isRename)
            commands.Add(new RenameNodeCommand(structureService, oldFullName, newFullName));
        if (result.Description != oldDescription)
            commands.Add(new NodeEditCommand(NodeId.FromName(newFullName)) { Description = result.Description });

        if (commands.Count == 0)
            return;

        commandService.Do(commands.Count == 1 ? commands[0] : new CompositeCommand([.. commands]));

        if (isRename)
        {
            // A rename replaces the node, so the previous selection (old id) is stale.
            selectionService.Unselect();
            selectionService.Select(NodeId.FromName(newFullName));
        }
    }

    async Task<NoteDialogResult?> ShowDialogAsync(string id, string description, bool isEdit)
    {
        var parameters = new DialogParameters
        {
            { nameof(NoteDialog.Id), id },
            { nameof(NoteDialog.Description), description },
            { nameof(NoteDialog.IsEdit), isEdit },
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
        };

        var dialog = await dialogService.ShowAsync<NoteDialog>(null, parameters, options);
        var dialogResult = await dialog.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return null;

        return dialogResult.Data as NoteDialogResult;
    }

    // The container the note is added into (mirrors ManualEditService.ResolveContainer): the
    // clicked container node when it is shown as an expanded box, else the clicked node's parent,
    // else the root.
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

    // A note's identity name: the parent's full name and the typed short id joined by a dot
    // (mirroring parsed/manual node names). Top-level notes (empty parent) keep just the short id.
    static string ComposeFullName(string parentName, string shortId) =>
        string.IsNullOrEmpty(parentName) ? shortId : $"{parentName}.{shortId}";

    // The next unused numeric id (1, 2, 3, …) among existing notes; user-entered ids may be any
    // short text but the default keeps guiding the reading order.
    static string NextNoteId(IModel model)
    {
        var maxNumber = 0;
        foreach (var node in model.Nodes.Values)
        {
            if (node.IsNote && int.TryParse(node.ShortName, out var number) && number > maxNumber)
                maxNumber = number;
        }
        return (maxNumber + 1).ToString();
    }

    static double SnapToGrid(double value) => Math.Round(value / NodeGrid.SnapSize) * NodeGrid.SnapSize;
}
