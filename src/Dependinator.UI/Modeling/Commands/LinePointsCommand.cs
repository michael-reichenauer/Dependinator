using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Commands;

// Sets the points of several lines at once, keeping whether the user set them (unlike
// LineEditCommand, whose points are the user's): the layout's bends routed again after a move,
// or a moved group's lines moved along. Execute stores the previous points; Revert puts them back.
class LinePointsCommand(IReadOnlyDictionary<LineId, IReadOnlyList<Pos>> points) : Command
{
    readonly string mergeKey = Guid.NewGuid().ToString();
    IReadOnlyDictionary<LineId, IReadOnlyList<Pos>> points = points;
    IReadOnlyDictionary<LineId, IReadOnlyList<Pos>>? pointsCopy;

    // A step of its own, or part of the move it follows (ICommandService.DoInStepOf).
    public override string MergeKey => mergeKey;

    public override void Execute(IModel model)
    {
        pointsCopy = Swap(model, points);
    }

    public override void Revert(IModel model)
    {
        if (pointsCopy is null)
            return;
        points = Swap(model, pointsCopy);
        pointsCopy = null;
    }

    // Sets the points and returns the ones they replaced.
    static Dictionary<LineId, IReadOnlyList<Pos>> Swap(IModel model, IReadOnlyDictionary<LineId, IReadOnlyList<Pos>> to)
    {
        var from = new Dictionary<LineId, IReadOnlyList<Pos>>();
        foreach (var (lineId, linePoints) in to)
        {
            if (!model.Lines.TryGetValue(lineId, out var line))
                continue;
            from[lineId] = [.. line.SegmentPoints];
            line.SetSegmentPoints(linePoints);
        }
        return from;
    }
}
