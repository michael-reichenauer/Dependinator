using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Sets a line's user label ("" clears it). Execute stores the previous label in LabelCopy;
// Revert swaps it back, so Execute/Revert can be replayed for redo/undo. Its own command type
// keeps a label edit from merging with the line's bend-point edits (LineEditCommand).
class LineLabelCommand(LineId lineId) : Command
{
    readonly LineId lineId = lineId;

    public override string MergeKey => lineId.Value;

    public string? Label { get; set; }
    public string? LabelCopy { get; private set; }

    public override void Execute(IModel model)
    {
        if (!model.Lines.TryGetValue(lineId, out var line) || Label is null)
            return;

        LabelCopy = line.Label ?? "";
        line.SetLabel(Label == "" ? null : Label);
    }

    public override void Revert(IModel model)
    {
        if (!model.Lines.TryGetValue(lineId, out var line) || LabelCopy is null)
            return;

        // Keep the applied value in Label so a redo re-applies it; restore the previous one.
        Label = line.Label ?? "";
        line.SetLabel(LabelCopy == "" ? null : LabelCopy);
        LabelCopy = null;
    }
}
