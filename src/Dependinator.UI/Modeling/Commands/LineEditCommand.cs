using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Commands;

class LineEditCommand(LineId lineId) : Command
{
    readonly LineId lineId = lineId;

    public IReadOnlyList<Pos>? SegmentPoints { get; set; }
    public IReadOnlyList<Pos>? SegmentPointsCopy { get; set; }
    public bool? IsSegmentsUserSetCopy { get; set; }

    public override void Execute(IModel model)
    {
        if (!model.Lines.TryGetValue(lineId, out var line) || SegmentPoints is null)
            return;

        SegmentPointsCopy = [.. line.SegmentPoints];
        IsSegmentsUserSetCopy = line.IsSegmentsUserSet;
        line.SetSegmentPoints(SegmentPoints);
        // Removing the last point hands the line back to auto-routing
        line.IsSegmentsUserSet = SegmentPoints.Count > 0;
    }

    public override void Revert(IModel model)
    {
        if (!model.Lines.TryGetValue(lineId, out var line) || SegmentPointsCopy is null)
            return;

        (SegmentPoints, SegmentPointsCopy) = (SegmentPointsCopy, SegmentPoints);
        line.SetSegmentPoints(SegmentPoints);
        (line.IsSegmentsUserSet, IsSegmentsUserSetCopy) = (IsSegmentsUserSetCopy ?? false, line.IsSegmentsUserSet);
    }
}
