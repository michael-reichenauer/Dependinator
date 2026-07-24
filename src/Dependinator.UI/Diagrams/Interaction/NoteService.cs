using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using MudBlazor;

namespace Dependinator.UI.Diagrams.Interaction;

// Result returned by NoteDialog: the (possibly edited) id and description, or a request to delete.
record NoteDialogResult(string Id, string Description, bool Delete);

// A note shown in the notes sidebar.
record NoteItem(NodeId NodeId, string Id, string Description);

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

    // The toggleable notes sidebar.
    bool IsShowSidebar { get; }
    void ToggleSidebar();

    // All notes, ordered by id (numbers numerically first, then letters), for the sidebar.
    IReadOnlyList<NoteItem> GetNotes();

    // Focuses a note from the sidebar: selects it and pans/zooms the diagram to it.
    Task ShowNoteAsync(NodeId nodeId);
}

[Scoped]
class NoteService(
    IModelMgr modelMgr,
    ICommandService commandService,
    IStructureService structureService,
    ISelectionService selectionService,
    IDialogService dialogService,
    INavigationService navigationService,
    IApplicationEvents applicationEvents
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

    public bool IsShowSidebar { get; private set; }

    public void ToggleSidebar()
    {
        IsShowSidebar = !IsShowSidebar;
        applicationEvents.TriggerUIStateChanged();
    }

    public IReadOnlyList<NoteItem> GetNotes()
    {
        using var model = modelMgr.UseModel();
        return model
            .Nodes.Values.Where(n => n.IsNote)
            .Select(n => new NoteItem(n.Id, n.ShortName, n.Description ?? ""))
            .OrderBy(n => n.Id, NoteIdComparer.Instance)
            .ToList();
    }

    public async Task ShowNoteAsync(NodeId nodeId)
    {
        selectionService.Unselect();
        await selectionService.Select(nodeId);
        await navigationService.ShowNodeAsync(nodeId);
    }

    // Orders note ids so numeric ids sort numerically and before any non-numeric ids (e.g.
    // 1, 2, …, 10, then A, B), matching the reading order the ids convey.
    sealed class NoteIdComparer : IComparer<string>
    {
        public static readonly NoteIdComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var xIsNum = int.TryParse(x, out var xn);
            var yIsNum = int.TryParse(y, out var yn);
            if (xIsNum && yIsNum)
                return xn.CompareTo(yn);
            if (xIsNum != yIsNum)
                return xIsNum ? -1 : 1;
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
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
            // Resolve the parent the same way manual-node add does. This keeps the note sized
            // relative to and scaling/hiding with the container it is dropped into.
            var container = DiagramPlacement.ResolveContainer(model, PointerId.Parse(e.TargetId));
            var local = DiagramPlacement.ToContainerLocal(model, container, e);

            boundary = new Rect(
                NodeGrid.Snap(local.X - NoteSize / 2),
                NodeGrid.Snap(local.Y - NoteSize / 2),
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
        var name = DiagramPlacement.ComposeFullName(parentName, result.Id);
        using (var model = modelMgr.UseModel())
        {
            if (model.Nodes.ContainsKey(NodeId.FromName(name)))
                return; // Id already used under this parent; the model is the source of truth.
        }

        commandService.Do(
            new AddNodeCommand(name, parentName, boundary, isNote: true, description: result.Description)
        );
        await selectionService.Select(NodeId.FromName(name));
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
        var newFullName = DiagramPlacement.ComposeFullName(parentName, result.Id);
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
            await selectionService.Select(NodeId.FromName(newFullName));
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
}
