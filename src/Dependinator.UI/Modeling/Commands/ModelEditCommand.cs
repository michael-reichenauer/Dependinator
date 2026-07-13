using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Commands;

// Edits the diagram viewport (zoom and/or pan offset). A null property means "not part of this
// command". Execute stores the previous value in the matching *Copy property; Revert swaps it
// back, so Execute/Revert can be replayed for redo/undo.
class ModelEditCommand : Command
{
    public double? Zoom { get; set; }
    public double? ZoomCopy { get; private set; }
    public Pos? Offset { get; set; }
    public Pos? OffsetCopy { get; private set; }

    public override void Execute(IModel model)
    {
        if (Zoom != null)
            (ZoomCopy, model.Zoom) = (model.Zoom, Zoom.Value);
        if (Offset != null)
            (OffsetCopy, model.Offset) = (model.Offset, Offset);
    }

    public override void Revert(IModel model)
    {
        if (ZoomCopy != null)
            (Zoom, model.Zoom, ZoomCopy) = (model.Zoom, ZoomCopy.Value, null);
        if (OffsetCopy != null)
            (Offset, model.Offset, OffsetCopy) = (model.Offset, OffsetCopy, null);
    }
}
