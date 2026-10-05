using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

// Browser-style history of the diagram view. A jump (search result, explorer row, breadcrumb,
// double-click, Fit to Screen, reveal from the editor) records where the user was, so Back
// returns there and Forward re-applies the jump. Free pan/zoom only changes the current view,
// exactly like scrolling a page does not add browser history. Edits are a separate concern
// (the undo stack); the view is deliberately not part of it.
interface IViewHistoryService
{
    bool CanGoBack { get; }
    bool CanGoForward { get; }

    // Records the current view as the place Back returns to; call right before a jump.
    void RecordJump();
    Task GoBackAsync();
    Task GoForwardAsync();

    // Forgets the history, e.g. when another model is opened.
    void Clear();
}

readonly record struct ViewEntry(Pos Offset, double Zoom);

[Scoped]
class ViewHistoryService(IModelMgr modelMgr, IPanZoomService panZoomService, IApplicationEvents applicationEvents)
    : IViewHistoryService
{
    const int MaxEntries = 50;

    readonly List<ViewEntry> back = [];
    readonly List<ViewEntry> forward = [];

    public bool CanGoBack => back.Count > 0;
    public bool CanGoForward => forward.Count > 0;

    public void RecordJump()
    {
        if (CurrentView() is not { } current)
            return;
        if (back.Count > 0 && back[^1] == current)
            return; // Jumping again from the same place adds nothing

        back.Add(current);
        if (back.Count > MaxEntries)
            back.RemoveAt(0);
        forward.Clear();
        applicationEvents.TriggerUIStateChanged();
    }

    public Task GoBackAsync() => MoveAsync(back, forward);

    public Task GoForwardAsync() => MoveAsync(forward, back);

    public void Clear()
    {
        if (back.Count == 0 && forward.Count == 0)
            return;
        back.Clear();
        forward.Clear();
        applicationEvents.TriggerUIStateChanged();
    }

    // Pops the target from one list, keeps the current view on the other, and animates there.
    async Task MoveAsync(List<ViewEntry> from, List<ViewEntry> to)
    {
        if (from.Count == 0)
            return;

        var target = from[^1];
        from.RemoveAt(from.Count - 1);
        if (CurrentView() is { } current && current != target)
            to.Add(current);
        applicationEvents.TriggerUIStateChanged();

        await panZoomService.PanZoomToViewAsync(target.Offset, target.Zoom);
    }

    ViewEntry? CurrentView()
    {
        using var model = modelMgr.UseModel();
        if (model.Zoom <= 0 || model.Offset == Pos.None)
            return null;
        return new ViewEntry(model.Offset, model.Zoom);
    }
}
