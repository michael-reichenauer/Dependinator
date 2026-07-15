using System.Web;
using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Models;

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
        (LongName, ShortName) = NodeName.GetDisplayNames(Name, Type, IsExecutable ?? false);
        HtmlShortName = HttpUtility.HtmlEncode(ShortName);
        HtmlLongName = HttpUtility.HtmlEncode(LongName);
    }

    public const double DefaultContainerZoom = 1.0 / 8;

    public NodeId Id { get; }
    public string Name { get; }
    public Node Parent { get; private set; }
    NodeType type = NodeType.None;
    public DateTime UpdateStamp { get; set; }
    public bool? IsPrivate { get; set; }
    bool? isExecutable;
    FileSpan? fileSpan;

    // For assembly nodes: the project builds an executable; shown as "Name (exe)" instead
    // of "Name (dll)". Affects the display names, so they are recomputed on change.
    public bool? IsExecutable
    {
        get => isExecutable;
        set
        {
            isExecutable = value;
            SetDisplayNames();
        }
    }

    public FileSpan? FileSpan => fileSpan;
    public FileSpan? FileSpanOrParentSpan => // Allow using a few levels up
        fileSpan ?? Parent?.fileSpan ?? Parent?.Parent?.fileSpan ?? Parent?.Parent?.Parent?.fileSpan;

    public NodeType Type
    {
        get => type;
        set
        {
            type = value;
            SetDisplayNames();
        }
    }

    // Only set via SetDescription, which keeps the pre-encoded HtmlDescription in sync.
    public string? Description { get; private set; }

    public string Color { get; set; } = "";

    // User-selected icon name (an IconLibrary id); null means the node-type default icon.
    public string? CustomIconName { get; set; }

    // User-selected icon tint (an IconLibrary.IconColors name); null means the default violet.
    // Independent of Color, which is the node container's own (background) palette color.
    public string? CustomIconColor { get; set; }

    // User-selected container color (a DColors.CustomNodeColors name); null means the
    // auto-assigned Color above. Independent of CustomIconColor.
    public string? CustomColor { get; set; }

    public double StrokeWidth { get; set; } = 2;
    public bool IsSelected { get; set; } = false;
    public bool IsEditMode { get; set; } = false;
    public bool IsChildrenLayoutRequired { get; set; } = false;
    public bool IsChildrenLayoutCustomized { get; set; } = false;

    Rect boundary = Rect.None;

    // A pass-through node is an invisible container that always exactly covers its parent's
    // inner viewport, so its children appear to be shown directly in the parent (e.g. the
    // "Dependinator.Core" namespace chain inside the "Dependinator.Core.dll" assembly node).
    // Its boundary is derived, never stored, so it self-syncs when the parent is resized,
    // panned, or zoomed.
    public Rect Boundary
    {
        get => IsPassThrough ? GetPassThroughBoundary() : boundary;
        set => boundary = value;
    }

    public double ContainerZoom { get; set; } = DefaultContainerZoom;
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
    public string? HtmlDescription { get; private set; }
    public bool IsHidden => IsUserSetHidden || IsParentSetHidden;
    public bool IsUserSetHidden { get; set; }
    public bool IsParentSetHidden { get; set; }
    public bool IsPassThrough { get; set; }

    // A manually added node (drawn by the user to design intended structure), as opposed to a
    // node produced by parsing. Manual nodes are marked visually and are exempt from the
    // stale-node removal that runs after each re-parse (see StructureService.ClearNotUpdated).
    public bool IsManual { get; set; }

    // A note is a user-drawn annotation rendered as a small circle showing a short id (e.g. "1",
    // "A"); its Description is shown as a hover tooltip. Notes are manual nodes (also IsManual) so
    // they persist and survive re-parse, but render via NoteSvg instead of the normal node chrome.
    public bool IsNote { get; set; }

    // Sets the description and keeps the pre-encoded HtmlDescription in sync; the single write
    // path for descriptions (note edits, SetFromDto, Update).
    public void SetDescription(string? description)
    {
        Description = string.IsNullOrEmpty(description) ? null : description;
        HtmlDescription = Description is not null ? HttpUtility.HtmlEncode(Description) : null;
    }

    public NodeDto ToDto() =>
        new()
        {
            Name = Name,
            ParentName = Parent?.Name ?? "",
            Type = Type.ToString(),
            Properties = new()
            {
                Description = string.IsNullOrEmpty(Description) ? null : Description,
                IsPrivate = IsPrivate,
                IsExecutable = IsExecutable,
            },
            Boundary = boundary != Rect.None ? boundary : null,
            Offset = ContainerOffset != Pos.None ? ContainerOffset : null,
            Zoom = ContainerZoom != DefaultContainerZoom ? ContainerZoom : null,
            Color = Color,
            CustomColor = CustomColor,
            IconName = CustomIconName,
            IconColor = CustomIconColor,
            IsUserSetHidden = IsUserSetHidden,
            IsParentSetHidden = IsParentSetHidden,
            IsChildrenLayoutCustomized = IsChildrenLayoutCustomized,
            IsManual = IsManual,
            IsNote = IsNote,
        };

    public void SetFromDto(NodeDto dto)
    {
        Type = Enums.To<NodeType>(dto.Type, NodeType.None);

        SetDescription(dto.Properties.Description);
        IsPrivate = dto.Properties.IsPrivate;
        IsExecutable = dto.Properties.IsExecutable;

        Boundary = dto.Boundary ?? Rect.None;
        ContainerOffset = dto.Offset ?? Pos.None;
        ContainerZoom = dto.Zoom ?? DefaultContainerZoom;
        Color = dto.Color ?? Color;
        CustomColor = dto.CustomColor;
        CustomIconName = dto.IconName;
        CustomIconColor = dto.IconColor;
        IsUserSetHidden = dto.IsUserSetHidden;
        IsParentSetHidden = dto.IsParentSetHidden;
        IsChildrenLayoutCustomized = dto.IsChildrenLayoutCustomized;
        IsManual = dto.IsManual;
        IsNote = dto.IsNote;
    }

    public void Update(Parsing.Node node)
    {
        Type = node.Properties.Type ?? Type;
        IsPrivate = node.Properties.IsPrivate ?? IsPrivate;
        IsExecutable = node.Properties.IsExecutable ?? IsExecutable;
        SetDescription(
            node.Properties.Description == NoValue.String ? null : node.Properties.Description ?? Description
        );
        fileSpan = node.Properties.FileSpan == NoValue.FileSpan ? null : node.Properties.FileSpan ?? fileSpan;
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

    // The parent's visible viewport expressed in the parent's inner (children) coordinate
    // space; recurses naturally when the parent is itself pass-through.
    Rect GetPassThroughBoundary()
    {
        var parentBoundary = Parent.Boundary;
        return new Rect(
            -Parent.ContainerOffset.X / Parent.ContainerZoom,
            -Parent.ContainerOffset.Y / Parent.ContainerZoom,
            parentBoundary.Width / Parent.ContainerZoom,
            parentBoundary.Height / Parent.ContainerZoom
        );
    }

    public double GetZoom()
    {
        var zoom = 1.0;
        this.Ancestors().ForEach(n => zoom *= 1 / n.ContainerZoom);
        return zoom;
    }

    internal Rect GetTotalBounds()
    {
        if (Children.Count == 0)
            return Rect.None;

        if (IsChildrenLayoutRequired)
            NodeLayout.AdjustChildren(this);
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
