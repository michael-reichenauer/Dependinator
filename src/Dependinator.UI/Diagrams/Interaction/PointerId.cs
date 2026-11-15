using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams.Interaction;

record PointerId
{
    public string ElementId { get; init; } = "";
    public string Id { get; init; } = "";
    public NodeId NodeId => string.IsNullOrEmpty(Id) ? NodeId.Empty : NodeId.FromId(Id);
    public LineId LineId => string.IsNullOrEmpty(Id) ? LineId.Empty : LineId.FromId(Id);
    public string SubId { get; init; } = "";
    public NodeResizeType NodeResizeType { get; init; } = NodeResizeType.None;
    public int LinePointIndex { get; init; } = -1;

    // The hollow handle on a selected line's segment (sub-id "lm{i}_{percent}": segment i runs
    // from point i to point i + 1 of the line's polyline, and the handle sits that far along it,
    // usually halfway). An edit-mode drag on it adds a bend point there (see
    // LineSvg.SelectedLineSvg); otherwise it is the line (IsLine).
    public int LineMidPointIndex { get; init; } = -1;
    public double LineMidPointFraction { get; init; } = 0.5;

    // A press on a line's label (its hit rect, sub-id "ll"). It is the line in every other
    // respect (select, double-click, menu, hover card); only an edit-mode drag moves the label.
    public bool IsLineLabel { get; init; }

    private PointerId(
        string id,
        string subId,
        NodeResizeType nodeResizeType,
        int linePointIndex = -1,
        int lineMidPointIndex = -1
    )
    {
        ElementId = $"{id}.{subId}";
        Id = id;
        SubId = subId;
        NodeResizeType = nodeResizeType;
        LinePointIndex = linePointIndex;
        LineMidPointIndex = lineMidPointIndex;
    }

    // The id of the svg canvas element itself (drops/clicks on the diagram background).
    public const string CanvasElementId = "svgcanvas";

    public static readonly PointerId Empty = new("", "", NodeResizeType.None);

    public static PointerId FromNode(NodeId nodeId) => new(nodeId.Value, "n", NodeResizeType.None);

    public static PointerId FromLine(LineId nodeId) => new(nodeId.Value, "l", NodeResizeType.None);

    // The element id of a line label's hit rect, which Parse reads back as the line with IsLineLabel.
    public static string LineLabelElementId(LineId lineId) => $"{lineId.Value}.{LineLabelSubId}";

    const string LineLabelSubId = "ll";

    public static PointerId FromLinePoint(LineId lineId, int pointIndex) =>
        new(lineId.Value, $"lp{pointIndex}", NodeResizeType.None, pointIndex);

    public static PointerId FromLineMidPoint(LineId lineId, int segmentIndex, int percent = 50) =>
        new(lineId.Value, $"lm{segmentIndex}_{percent}", NodeResizeType.None, lineMidPointIndex: segmentIndex)
        {
            LineMidPointFraction = percent / 100.0,
        };

    public static PointerId FromNodeResize(NodeId nodeId, NodeResizeType resizeType) =>
        new(nodeId.Value, ToSubId(resizeType), resizeType);

    public static PointerId FromNodeLinkHandle(NodeId nodeId) => new(nodeId.Value, "lh", NodeResizeType.None);

    // The "+ N private" row of a node's member card (MemberCard.HasPrivateToggle).
    public static PointerId FromPrivateToggle(NodeId nodeId) => new(nodeId.Value, "pt", NodeResizeType.None);

    public static PointerId Parse(string elementId)
    {
        var parts = elementId.Split('.');

        var id = parts[0];
        var subId = parts.Length > 1 ? parts[1] : "";
        if (subId == LineLabelSubId)
            return FromLine(LineId.FromId(id)) with { IsLineLabel = true };
        if (TryParseLineMidPoint(subId, out var segmentIndex, out var percent))
            return FromLineMidPoint(LineId.FromId(id), segmentIndex, percent);
        return new(id, subId, ToNodeResizeType(subId), ToLinePointIndex(subId));
    }

    // A press on the label is the same target as a press on its line (IsLineLabel is left out).
    public virtual bool Equals(PointerId? other) =>
        other is not null
        && ElementId == other.ElementId
        && Id == other.Id
        && SubId == other.SubId
        && NodeResizeType == other.NodeResizeType
        && LinePointIndex == other.LinePointIndex
        && LineMidPointIndex == other.LineMidPointIndex
        && LineMidPointFraction == other.LineMidPointFraction;

    public override int GetHashCode() =>
        HashCode.Combine(ElementId, Id, SubId, NodeResizeType, LinePointIndex, LineMidPointIndex);

    public bool IsNode => SubId == "n";
    public bool IsCanvas => Id == CanvasElementId;
    public bool IsLinkHandle => SubId == "lh";
    public bool IsPrivateToggle => SubId == "pt";
    public bool IsResize => NodeResizeType != NodeResizeType.None;

    // A segment handle lies on its line and is the line to everything but a drag (menus, hover
    // card, double-click).
    public bool IsLine => SubId == "l" || IsLineMidPoint;
    public bool IsLinePoint => LinePointIndex >= 0;
    public bool IsLineMidPoint => LineMidPointIndex >= 0;

    static NodeResizeType ToNodeResizeType(string subId) =>
        subId switch
        {
            "tl" => NodeResizeType.TopLeft,
            "tm" => NodeResizeType.TopMiddle,
            "tr" => NodeResizeType.TopRight,
            "ml" => NodeResizeType.MiddleLeft,
            "mr" => NodeResizeType.MiddleRight,
            "bl" => NodeResizeType.BottomLeft,
            "bm" => NodeResizeType.BottomMiddle,
            "br" => NodeResizeType.BottomRight,
            _ => NodeResizeType.None,
        };

    static int ToLinePointIndex(string subId)
    {
        if (!subId.StartsWith("lp", StringComparison.Ordinal))
            return -1;

        return int.TryParse(subId[2..], out var index) ? index : -1;
    }

    static bool TryParseLineMidPoint(string subId, out int segmentIndex, out int percent)
    {
        segmentIndex = -1;
        percent = 0;
        if (!subId.StartsWith("lm", StringComparison.Ordinal))
            return false;
        var parts = subId[2..].Split('_');
        return parts.Length == 2
            && int.TryParse(parts[0], out segmentIndex)
            && segmentIndex >= 0
            && int.TryParse(parts[1], out percent)
            && percent is > 0 and < 100;
    }

    static string ToSubId(NodeResizeType resizeType) =>
        resizeType switch
        {
            NodeResizeType.TopLeft => "tl",
            NodeResizeType.TopMiddle => "tm",
            NodeResizeType.TopRight => "tr",
            NodeResizeType.MiddleLeft => "ml",
            NodeResizeType.MiddleRight => "mr",
            NodeResizeType.BottomLeft => "bl",
            NodeResizeType.BottomMiddle => "bm",
            NodeResizeType.BottomRight => "br",
            NodeResizeType.None => "",
            _ => throw new ArgumentOutOfRangeException(nameof(resizeType), resizeType, null),
        };
}
