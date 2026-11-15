using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Commands;

// Sets the points of several lines at once (unlike LineEditCommand, whose points are the
// user's): the layout's bends routed again after a move, a moved group's lines moved along, or
// lines routed again by "Re-route line" and "Reset Lines". It keeps whether the user set the
// points, unless isLayoutSet hands the lines back to the layout (their bends no longer the
// user's). Execute stores the previous state; Revert puts it back.
class LinePointsCommand(IReadOnlyDictionary<LineId, IReadOnlyList<Pos>> points, bool isLayoutSet = false) : Command
{
    readonly string mergeKey = Guid.NewGuid().ToString();
    readonly IReadOnlyDictionary<LineId, IReadOnlyList<Pos>> points = points;
    Dictionary<LineId, (IReadOnlyList<Pos> Points, bool IsUserSet)>? before;

    // A step of its own, or part of the move it follows (ICommandService.DoInStepOf).
    public override string MergeKey => mergeKey;

    public override void Execute(IModel model)
    {
        before = [];
        foreach (var (lineId, linePoints) in points)
        {
            if (!model.Lines.TryGetValue(lineId, out var line))
                continue;
            before[lineId] = ([.. line.SegmentPoints], line.IsSegmentsUserSet);
            line.SetSegmentPoints(linePoints);
            if (isLayoutSet)
                line.IsSegmentsUserSet = false;
        }
    }

    public override void Revert(IModel model)
    {
        if (before is null)
            return;
        foreach (var (lineId, (linePoints, isUserSet)) in before)
        {
            if (!model.Lines.TryGetValue(lineId, out var line))
                continue;
            line.SetSegmentPoints(linePoints);
            line.IsSegmentsUserSet = isUserSet;
        }
        before = null;
    }
}
