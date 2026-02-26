namespace Dependinator.Models;

class LineEditCommand : Command
{
    readonly LineId lineId;

    public IReadOnlyList<Pos>? SegmentPoints { get; set; }
    public IReadOnlyList<Pos>? SegmentPointsCopy { get; set; }

    public LineEditCommand(LineId lineId)
    {
        this.lineId = lineId;
    }

    public override void Execute(IModel model)
    {
        if (!model.TryGetLine(lineId, out var line) || SegmentPoints is null)
            return;

        SegmentPointsCopy = [.. line.SegmentPoints];
        line.SetSegmentPoints(SegmentPoints);
    }

    public override void Unexecute(IModel model)
    {
        if (!model.TryGetLine(lineId, out var line) || SegmentPointsCopy is null)
            return;

        (SegmentPoints, SegmentPointsCopy) = (SegmentPointsCopy, SegmentPoints);
        line.SetSegmentPoints(SegmentPoints);
    }
}
