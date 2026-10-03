using Dependinator.Core;
using Dependinator.UI.Diagrams.Dependencies;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

// What the user right-clicked, which decides the menu's items.
enum ContextMenuTarget
{
    Canvas,
    Node,
    Line,
}

// Backs the right-click context menu: remembers where the user right-clicked (the pointer event,
// used to place a new note/node at that spot and into whatever container is under the cursor, or
// the node/line to act on) and dispatches the chosen action. The Canvas renders the menu while
// IsOpen is true.
interface IContextMenuService
{
    bool IsOpen { get; }
    Pos ScreenPos { get; }
    ContextMenuTarget Target { get; }
    string TargetName { get; }
    bool IsTargetManualNode { get; }
    bool IsTargetDirectLine { get; }
    bool CanShowTargetSource { get; }
    event Action? StateChanged;

    // Opens the menu at the right-clicked position (remembering the event for the actions below).
    void Open(PointerEvent e);
    void Close();

    // Canvas: adds a note / a manual node at the remembered right-click position.
    void AddNoteHere();
    void AddNodeHere();

    // Node and line: analysis and navigation on the right-clicked item.
    Task ShowReferencesAsync();
    Task ShowDependenciesAsync();
    Task ZoomToAsync();
    Task CopyNodeLinkAsync();
    Task CopyViewLinkAsync();
    Task ShowSourceAsync();
    void HideDirectLine();

    // Manual node: editing.
    Task RenameNodeAsync();
    void DeleteNode();
}

[Scoped]
class ContextMenuService(
    INoteService noteService,
    IManualEditService manualEditService,
    ISelectionService selectionService,
    IDependenciesService dependenciesService,
    INavigationService navigationService,
    IShareLinkService shareLinkService,
    IModelMgr modelMgr,
    IApplicationEvents applicationEvents
) : IContextMenuService
{
    // The right-click event, kept so the placement uses the same position/target the menu opened at.
    PointerEvent pendingEvent = new();
    PointerId targetId = PointerId.Empty;

    public bool IsOpen { get; private set; }
    public Pos ScreenPos { get; private set; } = Pos.None;
    public ContextMenuTarget Target { get; private set; } = ContextMenuTarget.Canvas;
    public string TargetName { get; private set; } = "";
    public bool IsTargetManualNode { get; private set; }
    public bool IsTargetDirectLine { get; private set; }
    public bool CanShowTargetSource { get; private set; }
    public event Action? StateChanged;

    public void Open(PointerEvent e)
    {
        pendingEvent = e;
        targetId = PointerId.Parse(e.TargetId);
        ScreenPos = new Pos(e.ClientX, e.ClientY);
        ResolveTarget();
        IsOpen = true;
        StateChanged?.Invoke();
    }

    // A right-click on a node's own box (an icon, or an open container's chrome) targets that
    // node; a right-click inside a container's content area, on a note or on the canvas
    // targets the canvas (add here). A line targets the line.
    void ResolveTarget()
    {
        Target = ContextMenuTarget.Canvas;
        TargetName = "";
        IsTargetManualNode = false;
        IsTargetDirectLine = false;
        CanShowTargetSource = false;

        using var model = modelMgr.UseModel();
        if (targetId.IsNode && model.Nodes.TryGetValue(targetId.NodeId, out var node))
        {
            if (node.IsRoot || node.IsNote || NodeViewPolicy.IsContainerView(node, model.Zoom))
                return;
            Target = ContextMenuTarget.Node;
            TargetName = node.ShortName;
            IsTargetManualNode = node.IsManual;
            CanShowTargetSource = Build.IsVsCodeExtWasm && node.FileSpanOrParentSpan is not null;
            return;
        }

        if (targetId.IsLine && model.Lines.TryGetValue(LineId.FromId(targetId.Id), out var line))
        {
            Target = ContextMenuTarget.Line;
            TargetName = $"{line.Source.ShortName}→{line.Target.ShortName}";
            IsTargetDirectLine = line.IsDirect;
            CanShowTargetSource = Build.IsVsCodeExtWasm && line.Source.FileSpanOrParentSpan is not null;
        }
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        StateChanged?.Invoke();
    }

    public void AddNoteHere()
    {
        Close();
        _ = noteService.PlaceNoteAtAsync(pendingEvent);
    }

    public void AddNodeHere()
    {
        Close();
        _ = manualEditService.AddNodeAtAsync(pendingEvent);
    }

    public async Task ShowReferencesAsync()
    {
        Close();
        if (!await SelectTargetAsync())
            return;
        dependenciesService.ShowReferences();
    }

    public async Task ShowDependenciesAsync()
    {
        Close();
        if (!await SelectTargetAsync())
            return;
        dependenciesService.ShowDependencies();
    }

    public async Task ZoomToAsync()
    {
        Close();
        if (Target != ContextMenuTarget.Node)
            return;
        await navigationService.ShowNodeAsync(targetId.NodeId);
    }

    public async Task CopyNodeLinkAsync()
    {
        Close();
        if (Target != ContextMenuTarget.Node)
            return;
        await shareLinkService.CopyNodeLinkAsync(targetId.NodeId);
    }

    public async Task CopyViewLinkAsync()
    {
        Close();
        await shareLinkService.CopyViewLinkAsync();
    }

    public async Task ShowSourceAsync()
    {
        Close();
        if (!CanShowTargetSource)
            return;
        NodeId nodeId;
        using (var model = modelMgr.UseModel())
        {
            if (Target == ContextMenuTarget.Node)
                nodeId = targetId.NodeId;
            else if (model.Lines.TryGetValue(LineId.FromId(targetId.Id), out var line))
                nodeId = line.Source.Id;
            else
                return;
        }
        await navigationService.ShowEditor(nodeId);
    }

    public void HideDirectLine()
    {
        Close();
        if (Target != ContextMenuTarget.Line || !IsTargetDirectLine)
            return;
        dependenciesService.HideDirectLine(LineId.FromId(targetId.Id));
    }

    // The inline name field anchors at the selected node's toolbar position, so select first.
    public async Task RenameNodeAsync()
    {
        Close();
        if (Target != ContextMenuTarget.Node || !IsTargetManualNode)
            return;
        if (!await SelectTargetAsync())
            return;
        manualEditService.BeginRenameNode(targetId.NodeId, selectionService.SelectedNodePosition);
    }

    public void DeleteNode()
    {
        Close();
        if (Target != ContextMenuTarget.Node || !IsTargetManualNode)
            return;
        selectionService.Unselect();
        manualEditService.DeleteManualNode(targetId.NodeId);
        applicationEvents.TriggerUIStateChanged();
    }

    // The explorer and the toolbars act on the current selection, so select the item first.
    async Task<bool> SelectTargetAsync()
    {
        if (Target == ContextMenuTarget.Canvas)
            return false;
        if (selectionService.SelectedId != targetId)
            await selectionService.Select(targetId, pendingEvent);
        return selectionService.SelectedId == targetId;
    }
}
