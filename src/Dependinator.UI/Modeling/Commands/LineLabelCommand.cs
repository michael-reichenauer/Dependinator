using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Sets a line's user label and the description under it ("" clears either). Execute stores
// the previous texts; Revert swaps them back, so Execute/Revert can be replayed for redo/undo.
// Its own command type keeps a label edit from merging with the line's bend-point edits.
class LineLabelCommand(LineId lineId) : Command
{
    readonly LineId lineId = lineId;

    public override string MergeKey => lineId.Value;

    public string? Label { get; set; }
    public string? Description { get; set; }
    public string? LabelCopy { get; private set; }
    public string? DescriptionCopy { get; private set; }

    public override void Execute(IModel model)
    {
        if (!model.Lines.TryGetValue(lineId, out var line) || (Label is null && Description is null))
            return;

        LabelCopy = line.Label ?? "";
        DescriptionCopy = line.LabelDescription ?? "";
        line.SetLabel(Label ?? LabelCopy, Description ?? DescriptionCopy);
    }

    public override void Revert(IModel model)
    {
        if (!model.Lines.TryGetValue(lineId, out var line) || LabelCopy is null)
            return;

        // Keep the applied values so a redo re-applies them; restore the previous ones.
        Label = line.Label ?? "";
        Description = line.LabelDescription ?? "";
        line.SetLabel(LabelCopy, DescriptionCopy);
        LabelCopy = null;
        DescriptionCopy = null;
    }
}
