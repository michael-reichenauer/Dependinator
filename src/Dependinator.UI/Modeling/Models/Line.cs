using System.Web;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Models;

record LinePos(double X1, double Y1, double X2, double Y2);

class Line : IItem
{
    const double DirectStrokeWidth = 2;
    const double BaseStrokeWidth = 1;
    const double HiddenStrokeWidth = 1;
    const double AdditionalStrokeWidthPerLink = 0.05;
    const double MaxStrokeWidth = 3;

    readonly Dictionary<Id, Link> links = new();
    readonly List<Pos> segmentPoints = [];
    double strokeWidth = BaseStrokeWidth;
    bool isHidden;

    public Line(Node source, Node target, bool isDirect = false, LineId? id = null, bool isInheritance = false)
    {
        Source = source;
        Target = target;
        Id =
            id
            ?? (
                isDirect ? LineId.FromDirect(source.Name, target.Name)
                : isInheritance ? LineId.FromInheritance(source.Name, target.Name)
                : LineId.From(source.Name, target.Name)
            );
        IsDirect = isDirect;
        IsInheritance = isInheritance;
        UpdateStrokeWidth();
    }

    public ICollection<Link> Links => links.Values;
    public LineId Id { get; }
    public Node Source { get; }
    public Node Target { get; }
    public bool IsEmpty => links.Count == 0;
    public bool IsDirect { get; }
    public bool IsInheritance { get; }
    public Node? RenderAncestor { get; set; }

    // A cousin line crosses container boundaries between the "deepest visible representatives"
    // of its links' endpoints (see RepLineService); like a direct line, it is rendered inside
    // its RenderAncestor's coordinate space.
    public bool IsCousin => RenderAncestor is not null && !IsDirect;

    // Transient per-zoom render state: true when this line is the current representative line
    // for at least one link at the most recently synced zoom (set by RepLineService.Sync);
    // only active lines are rendered. Not persisted.
    public bool IsActiveRep { get; set; }

    // Split-line state (transient, never persisted): the user can "split" an aggregated line
    // one level into its target, temporarily showing dashed direct-style lines to the
    // target's children instead. SplitParent points from a split line back to the line it was
    // split from; SplitLines holds a split line's live children; IsSplitSuppressed hides the
    // parent while all its links are represented by split lines (see DependenciesService).
    public Line? SplitParent { get; set; }
    public List<Line> SplitLines { get; } = [];
    public bool IsSplitSuppressed { get; set; }

    // An inheritance line segment is only anchored specially (top/bottom middle) at an end that
    // is a real link endpoint; a shared segment can end at a container boundary at the other end.
    public bool HasInheritanceSourceEnd => IsInheritance && links.Values.Any(l => l.Source == Source);
    public bool HasInheritanceTargetEnd => IsInheritance && links.Values.Any(l => l.Target == Target);

    public double StrokeWidth => strokeWidth;
    public bool IsSelected { get; internal set; }
    public string HtmlShortName => $"{Source.HtmlShortName}→{Target.HtmlShortName}";
    public IReadOnlyList<Pos> SegmentPoints => segmentPoints;

    // True when the user has placed the segment points manually; auto-routing (LayeredLayout)
    // must not overwrite them. Auto-generated points leave this false so re-layout can redo them.
    public bool IsSegmentsUserSet { get; set; }

    public string? Description { get; private set; }
    public string? HtmlDescription { get; private set; }
    public DateTime DescriptionUpdateStamp { get; private set; }

    public bool IsHidden
    {
        get => isHidden;
        internal set
        {
            if (isHidden == value)
                return;
            isHidden = value;
            UpdateStrokeWidth();
        }
    }

    public bool Contains(Link link) => links.ContainsKey(link.Id);

    public void Add(Link link)
    {
        var isNewLink = !links.ContainsKey(link.Id);
        links[link.Id] = link;

        if (isNewLink)
            UpdateStrokeWidth();
    }

    public void Remove(Link link)
    {
        if (links.Remove(link.Id))
            UpdateStrokeWidth();
    }

    public void SetDescription(string? text, DateTime updateStamp)
    {
        Description = text;
        HtmlDescription = text is not null ? HttpUtility.HtmlEncode(text) : null;
        DescriptionUpdateStamp = updateStamp;
    }

    public void ClearDescription() => SetDescription(null, DateTime.MinValue);

    public override string ToString() => $"{Source}->{Target} ({links.Count})";

    public void SetSegmentPoints(IEnumerable<Pos> points)
    {
        segmentPoints.Clear();
        segmentPoints.AddRange(points);
    }

    void UpdateStrokeWidth()
    {
        if (IsDirect)
        {
            strokeWidth = DirectStrokeWidth;
            return;
        }
        if (isHidden)
        {
            strokeWidth = HiddenStrokeWidth;
            return;
        }

        var linkCount = links.Count;
        if (linkCount <= 1)
        {
            strokeWidth = BaseStrokeWidth;
            return;
        }

        var computedWidth = BaseStrokeWidth + (linkCount - 1) * AdditionalStrokeWidthPerLink;
        strokeWidth = Math.Min(computedWidth, MaxStrokeWidth);
    }
}
