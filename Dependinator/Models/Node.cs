using System.Text.Json.Serialization;
using System.Web;
using DependinatorCore.Parsing;

namespace Dependinator.Models;

[Serializable]
record NodeDto
{
    public required string Name { get; init; }
    public required string ParentName { get; init; }
    public required string Type { get; init; }
    public NodeAttributes Attributes { get; init; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Rect? Boundary { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double? Zoom { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Pos? Offset { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Color { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsUserSetHidden { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsParentSetHidden { get; set; }
}

[Serializable]
record NodeAttributes
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Description { get; init; }
    public bool IsPrivate { get; init; }
    public string? MemberType { get; set; }
}

class Node : IItem
{
    public Node(string name, Node parent)
    {
        Id = NodeId.FromName(name);
        Name = name;
        Parent = parent;

        Color = DColors.ColorBasedOnName(name);

        SetDisplayNames();
    }

    private void SetDisplayNames()
    {
        (LongName, ShortName) = NodeName.GetDisplayNames(Name, Type);
        HtmlShortName = HttpUtility.HtmlEncode(ShortName);
        HtmlLongName = HttpUtility.HtmlEncode(LongName);
    }

    public const double DefaultContainerZoom = 1.0 / 8;

    public NodeId Id { get; }
    public string Name { get; }
    public Node Parent { get; private set; }
    NodeType type = NodeType.None;
    public DateTime UpdateStamp { get; set; }
    public bool IsPrivate { get; set; }
    public MemberType MemberType { get; set; }
    public FileSpan? FileSpan { get; set; }
    public NodeType Type
    {
        get => type;
        set
        {
            type = value;
            SetDisplayNames();
        }
    }

    public string Description { get; set; } = "";

    public string Color { get; set; } = "";

    public double StrokeWidth { get; set; } = 2;
    public bool IsSelected { get; set; } = false;
    public bool IsEditMode { get; set; } = false;
    public bool IsChildrenLayoutRequired { get; set; } = false;

    public Rect Boundary { get; set; } = Rect.None;
    public Double ContainerZoom { get; set; } = DefaultContainerZoom;
    public Pos ContainerOffset { get; set; } = Pos.None;

    public List<Node> Children { get; } = new();
    public List<Link> SourceLinks { get; } = new();
    public List<Link> TargetLinks { get; } = new();
    public List<Line> SourceLines { get; } = new();
    public List<Line> TargetLines { get; } = new();
    public List<Line> DirectLines { get; } = new();

    public bool IsRoot => Type == NodeType.Root;
    public string LongName { get; private set; } = "";
    public string ShortName { get; private set; } = "";
    public string HtmlShortName { get; private set; } = "";
    public string HtmlLongName { get; private set; } = "";
    public bool IsHidden => IsUserSetHidden || IsParentSetHidden;
    public bool IsUserSetHidden { get; set; }
    public bool IsParentSetHidden { get; set; }

    public NodeDto ToDto() =>
        new()
        {
            Name = Name,
            ParentName = Parent?.Name ?? "",
            Type = Type.ToString(),
            Attributes = new()
            {
                Description = Description,
                IsPrivate = IsPrivate,
                MemberType = MemberType.ToString(),
            },
            Boundary = Boundary != Rect.None ? Boundary : null,
            Offset = ContainerOffset != Pos.None ? ContainerOffset : null,
            Zoom = ContainerZoom != DefaultContainerZoom ? ContainerZoom : null,
            Color = Color,
            IsUserSetHidden = IsUserSetHidden,
            IsParentSetHidden = IsParentSetHidden,
        };

    public void SetFromDto(NodeDto dto)
    {
        Type = Enums.To<NodeType>(dto.Type, NodeType.None);
        Description = dto.Attributes.Description ?? "";
        IsPrivate = dto.Attributes.IsPrivate;
        Boundary = dto.Boundary ?? Rect.None;
        ContainerOffset = dto.Offset ?? Pos.None;
        ContainerZoom = dto.Zoom ?? DefaultContainerZoom;
        Color = dto.Color ?? Color;
        IsUserSetHidden = dto.IsUserSetHidden;
        IsParentSetHidden = dto.IsParentSetHidden;
        MemberType = Enum.TryParse<MemberType>(dto.Attributes.MemberType, out var value) ? value : MemberType.None;
    }

    public void Update(Parsing.Node node)
    {
        Type = node.Attributes.Type ?? Type;
        IsPrivate = node.Attributes.IsPrivate ?? IsPrivate;
        Description = node.Attributes.Description ?? Description;
        MemberType = node.Attributes.MemberType ?? MemberType;
        FileSpan = node.Attributes.FileSpan ?? FileSpan;
    }

    public void SetHidden(bool hidden, bool isUserSet)
    {
        if (isUserSet)
        {
            IsUserSetHidden = hidden;
            Children.ForEach(child => child.SetHidden(hidden, false));
            return;
        }

        // Set by parent
        IsParentSetHidden = hidden;
        if (IsUserSetHidden)
            return;
        Children.ForEach(child => child.SetHidden(hidden, false));
    }

    public double GetZoom()
    {
        var zoom = 1.0;
        this.Ancestors().ForEach(n => zoom *= 1 / n.ContainerZoom);
        return zoom;
    }

    internal Rect GetTotalBounds()
    {
        // Calculate the total bounds of the children
        (double x1, double y1, double x2, double y2) = Children.Aggregate(
            (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue),
            (bounds, child) =>
            {
                var (x1, y1, x2, y2) = bounds;
                var cb = child.Boundary;
                var (cx1, cy1, cx2, cy2) = (cb.X, cb.Y, cb.X + cb.Width, cb.Y + cb.Height);
                return (Math.Min(x1, cx1), Math.Min(y1, cy1), Math.Max(x2, cx2), Math.Max(y2, cy2));
            }
        );

        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }

    public (Pos pos, double zoom) GetPosAndZoom()
    {
        if (IsRoot)
            return (new Pos(0, 0), 1.0);

        var (parentPos, parentZoom) = Parent.GetPosAndZoom();

        var zoom = Parent.ContainerZoom * parentZoom;

        var x = parentPos.X + Boundary.X * zoom + Parent.ContainerOffset.X * parentZoom;
        var y = parentPos.Y + Boundary.Y * zoom + Parent.ContainerOffset.Y * parentZoom;
        var pos = new Pos(x, y);

        return (pos, zoom);
    }

    public (Pos pos, double zoom) GetCenterPosAndZoom()
    {
        if (IsRoot)
            return (new Pos(0, 0), 1.0);

        var (pos, zoom) = GetPosAndZoom();

        var x = pos.X + Boundary.Width / 2 * zoom;
        var y = pos.Y + Boundary.Height / 2 * zoom;
        var centerPos = new Pos(x, y);

        return (centerPos, zoom);
    }

    public void AddChild(Node child)
    {
        if (child.Boundary == Rect.None)
        {
            IsChildrenLayoutRequired = true;
        }

        Children.Add(child);
        child.Parent = this;
    }

    public void RemoveChild(Node child)
    {
        Children.Remove(child);
        child.Parent = null!;
    }

    public bool AddSourceLink(Link link)
    {
        if (SourceLinks.Contains(link))
            return false;
        SourceLinks.Add(link);
        return true;
    }

    public void AddTargetLink(Link link)
    {
        if (TargetLinks.Contains(link))
            return;
        TargetLinks.Add(link);
    }

    public void Remove(Link link)
    {
        SourceLinks.Remove(link);
        TargetLinks.Remove(link);
    }

    public void Remove(Line line)
    {
        SourceLines.Remove(line);
        TargetLines.Remove(line);
        DirectLines.Remove(line);
    }

    public void AddDirectLine(Line line)
    {
        if (!DirectLines.Contains(line))
        {
            DirectLines.Add(line);
        }
    }

    public void RemoveDirectLine(Line line)
    {
        DirectLines.Remove(line);
    }

    public override string ToString() => IsRoot ? "<root>" : LongName;
}
