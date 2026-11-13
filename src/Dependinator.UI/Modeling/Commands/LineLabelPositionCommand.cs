using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Moves a line's label along the line (Line.LabelPosition). Execute stores the previous position;
// Revert swaps it back, so Execute/Revert can be replayed for redo/undo. A drag sends one command
// per pointer move, and they merge into one undo step (same type and line); its own type keeps a
// move from merging with a label text edit or a bend-point edit.
class LineLabelPositionCommand(LineId lineId) : Command
{
    readonly LineId lineId = lineId;

    public override string MergeKey => lineId.Value;

    public double Position { get; set; } = Line.DefaultLabelPosition;
    public double? PositionCopy { get; private set; }

    public override void Execute(IModel model)
    {
        if (!model.Lines.TryGetValue(lineId, out var line))
            return;

        PositionCopy = line.LabelPosition;
        line.SetLabelPosition(Position);
    }

    public override void Revert(IModel model)
    {
        if (!model.Lines.TryGetValue(lineId, out var line) || PositionCopy is not { } previous)
            return;

        Position = line.LabelPosition;
        line.SetLabelPosition(previous);
        PositionCopy = null;
    }
}
