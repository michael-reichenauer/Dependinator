using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using MudBlazor;

namespace Dependinator.UI.Diagrams.Interaction;

// Result returned by ZoneDialog: the (possibly edited) label and subtitle, or a request to delete.
record ZoneDialogResult(string Label, string Subtitle, bool Delete);

// Orchestrates zones (user-drawn, labeled background areas that group the nodes drawn over
// them): arming "place zone" mode from the menu, placing a zone at the clicked position (with a
// free label pre-filled), and editing or deleting an existing one. A zone is a manual Node
// (IsZone) parented into whatever container it is dropped in, like a note, so it scales and
// hides with that container. All mutations go through the undoable CommandService.
interface IZoneService
{
    // True while the user has chosen "Add zone" and the next canvas click places the zone.
    bool IsPlacingZone { get; }
    event Action? StateChanged;

    void BeginPlaceZone();
    void CancelPlaceZone();

    // Places a zone with its top-left corner at the clicked canvas position (opens the zone
    // dialog). Called by the interaction service on a click while IsPlacingZone is true.
    Task PlaceZoneAtAsync(PointerEvent e);

    // Opens the zone dialog to edit (or delete) an existing zone.
    Task EditZoneAsync(NodeId nodeId);

    bool IsZoneNode(NodeId nodeId);
}

[Scoped]
class ZoneService(
    IModelMgr modelMgr,
    ICommandService commandService,
    IStructureService structureService,
    ISelectionService selectionService,
    IDialogService dialogService,
    ISnackbar snackbar
) : IZoneService
{
    // A new zone's size in its container's child coordinates: room for a handful of nodes; the
    // user resizes it to fit the group.
    const double DefaultWidth = 320;
    const double DefaultHeight = 200;
    const string DefaultLabel = "Zone";

    public bool IsPlacingZone { get; private set; }
    public event Action? StateChanged;

    public void BeginPlaceZone()
    {
        IsPlacingZone = true;
        StateChanged?.Invoke();
    }

    public void CancelPlaceZone()
    {
        if (!IsPlacingZone)
            return;
        IsPlacingZone = false;
        StateChanged?.Invoke();
    }

    public bool IsZoneNode(NodeId nodeId)
    {
        using var model = modelMgr.UseModel();
        return model.Nodes.TryGetValue(nodeId, out var node) && node.IsZone;
    }

    public async Task PlaceZoneAtAsync(PointerEvent e)
    {
        IsPlacingZone = false;
        StateChanged?.Invoke();

        Rect boundary;
        string parentName;
        string defaultLabel;
        using (var model = modelMgr.UseModel())
        {
            // Resolve the parent the same way manual-node add does (a zone is never a container
            // itself, so a click on a zone adds into its parent, next to it).
            var container = DiagramPlacement.ResolveContainer(model, PointerId.Parse(e.TargetId));
            var local = DiagramPlacement.ToContainerLocal(model, container, e);

            boundary = new Rect(NodeGrid.Snap(local.X), NodeGrid.Snap(local.Y), DefaultWidth, DefaultHeight);
            parentName = container.Name;
            defaultLabel = DiagramPlacement.UniqueShortName(model, parentName, DefaultLabel);
        }

        var result = await ShowDialogAsync(defaultLabel, "", isEdit: false);
        if (result is null || result.Delete)
            return;

        // The label is the zone's short name; its identity is qualified by its parent (like
        // parsed and manual nodes), so the same label can be reused under different parents.
        var name = DiagramPlacement.ComposeFullName(parentName, result.Label);
        using (var model = modelMgr.UseModel())
        {
            if (model.Nodes.ContainsKey(NodeId.FromName(name)))
            {
                snackbar.Add(
                    $"A node named '{result.Label}' already exists here. Choose another label.",
                    Severity.Warning
                );
                return;
            }
        }

        commandService.Do(new AddNodeCommand(name, parentName, boundary, description: result.Subtitle, isZone: true));
        await selectionService.Select(NodeId.FromName(name));
    }

    public async Task EditZoneAsync(NodeId nodeId)
    {
        string oldLabel;
        string oldFullName;
        string parentName;
        string oldSubtitle;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node) || !node.IsZone)
                return;
            oldLabel = node.ShortName;
            oldFullName = node.Name;
            parentName = node.Parent?.Name ?? "";
            oldSubtitle = node.Description ?? "";
        }

        var result = await ShowDialogAsync(oldLabel, oldSubtitle, isEdit: true);
        if (result is null)
            return;

        if (result.Delete)
        {
            selectionService.Unselect();
            var command = new DeleteNodeCommand(nodeId);
            commandService.Do(command);
            snackbar.Add(
                "Zone deleted.",
                Severity.Normal,
                config =>
                {
                    config.Action = "Undo";
                    config.ActionColor = Color.Primary;
                    // Only this deletion: an edit made since must not be reverted by the snackbar.
                    config.OnClick = async _ =>
                    {
                        if (!await commandService.UndoIfLatest(command))
                            snackbar.Add("Changes were made since; use Undo (Ctrl+Z) to step back.", Severity.Info);
                    };
                }
            );
            return;
        }

        // Re-qualify the typed label under the zone's existing parent to get its new identity.
        var newFullName = DiagramPlacement.ComposeFullName(parentName, result.Label);
        var isRename = newFullName != oldFullName;
        if (isRename)
        {
            using var model = modelMgr.UseModel();
            if (model.Nodes.ContainsKey(NodeId.FromName(newFullName)))
            {
                snackbar.Add(
                    $"A node named '{result.Label}' already exists here. Choose another label.",
                    Severity.Warning
                );
                return;
            }
        }

        var commands = new List<Command>();
        if (isRename)
            commands.Add(new RenameNodeCommand(structureService, oldFullName, newFullName));
        if (result.Subtitle != oldSubtitle)
            commands.Add(new NodeEditCommand(NodeId.FromName(newFullName)) { Description = result.Subtitle });

        if (commands.Count == 0)
            return;

        commandService.Do(commands.Count == 1 ? commands[0] : new CompositeCommand([.. commands]));

        if (isRename)
        {
            // A rename replaces the node, so the previous selection (old name) is stale.
            selectionService.Unselect();
            await selectionService.Select(NodeId.FromName(newFullName));
        }
    }

    async Task<ZoneDialogResult?> ShowDialogAsync(string label, string subtitle, bool isEdit)
    {
        var parameters = new DialogParameters
        {
            { nameof(ZoneDialog.Label), label },
            { nameof(ZoneDialog.Subtitle), subtitle },
            { nameof(ZoneDialog.IsEdit), isEdit },
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
        };

        var dialog = await dialogService.ShowAsync<ZoneDialog>(null, parameters, options);
        var dialogResult = await dialog.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return null;

        return dialogResult.Data as ZoneDialogResult;
    }
}
