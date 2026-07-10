using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

// Backs the right-click context menu: remembers where the user right-clicked (the pointer event,
// used to place the new note/node at that spot and into whatever container is under the cursor) and
// dispatches the chosen action. The Canvas renders the menu while IsOpen is true.
interface IContextMenuService
{
    bool IsOpen { get; }
    Pos ScreenPos { get; }
    event Action? StateChanged;

    // Opens the menu at the right-clicked position (remembering the event for the actions below).
    void Open(PointerEvent e);
    void Close();

    // Adds a note / a manual node at the remembered right-click position.
    void AddNoteHere();
    void AddNodeHere();
}

[Scoped]
class ContextMenuService(INoteService noteService, IManualEditService manualEditService) : IContextMenuService
{
    // The right-click event, kept so the placement uses the same position/target the menu opened at.
    PointerEvent pendingEvent = new();

    public bool IsOpen { get; private set; }
    public Pos ScreenPos { get; private set; } = Pos.None;
    public event Action? StateChanged;

    public void Open(PointerEvent e)
    {
        pendingEvent = e;
        ScreenPos = new Pos(e.ClientX, e.ClientY);
        IsOpen = true;
        StateChanged?.Invoke();
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
        manualEditService.BeginAddNode(pendingEvent);
    }
}
