using System;

namespace Dependinator.Models;

record LinePos(double X1, double Y1, double X2, double Y2);

class Line : IItem
{
    const double DirectStrokeWidth = 2;
    const double BaseStrokeWidth = 1;
    const double HiddenStrokeWidth = 1;
    const double AdditionalStrokeWidthPerLink = 0.05;
    const double MaxStrokeWidth = 4;

    readonly Dictionary<Id, Link> links = new();
    double strokeWidth = BaseStrokeWidth;
    bool isHidden;

    public Line(Node source, Node target, bool isDirect = false, LineId? id = null)
    {
        Source = source;
        Target = target;
        Id = id ?? (isDirect ? LineId.FromDirect(source.Name, target.Name) : LineId.From(source.Name, target.Name));
        IsDirect = isDirect;
        UpdateStrokeWidth();
    }

    public ICollection<Link> Links => links.Values;
    public LineId Id { get; }
    public Node Source { get; }
    public Node Target { get; }
    public bool IsEmpty => links.Count == 0;
    public bool IsDirect { get; }
    public Node? RenderAncestor { get; set; }

    public double StrokeWidth => strokeWidth;
    public bool IsSelected { get; internal set; }
    public string HtmlShortName => $"{Source.HtmlShortName}→{Target.HtmlShortName}";

    public bool IsUpHill { get; internal set; } // Used to determine the direction of the line for placing the toolbar
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

    public override string ToString() => $"{Source}->{Target} ({links.Count})";

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
