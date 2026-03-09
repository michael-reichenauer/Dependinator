using Dependinator.Shared.Types;

namespace Dependinator.Models.Commands;

class LineEditCommand(LineId lineId) : Command
{
    readonly LineId lineId = lineId;

    public IReadOnlyList<Pos>? SegmentPoints { get; set; }
    public IReadOnlyList<Pos>? SegmentPointsCopy { get; set; }

    public override void Execute(IModel model)
    {
        if (!model.Lines.TryGetValue(lineId, out var line) || SegmentPoints is null)
            return;

        SegmentPointsCopy = [.. line.SegmentPoints];
        line.SetSegmentPoints(SegmentPoints);
    }

    public override void Revert(IModel model)
    {
        if (!model.Lines.TryGetValue(lineId, out var line) || SegmentPointsCopy is null)
            return;

        (SegmentPoints, SegmentPointsCopy) = (SegmentPointsCopy, SegmentPoints);
        line.SetSegmentPoints(SegmentPoints);
    }
}
