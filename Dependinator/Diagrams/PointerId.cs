using Dependinator.Models;

namespace Dependinator.Diagrams;

record PointerId
{
    public string ElementId { get; init; } = "";
    public string Id { get; init; } = "";
    public string SubId { get; init; } = "";
    public NodeResizeType NodeResizeType { get; init; } = NodeResizeType.None;
    public int LinePointIndex { get; init; } = -1;

    private PointerId(string id, string subId, NodeResizeType nodeResizeType, int linePointIndex = -1)
    {
        ElementId = $"{id}.{subId}";
        Id = id;
        SubId = subId;
        NodeResizeType = nodeResizeType;
        LinePointIndex = linePointIndex;
    }

    public static readonly PointerId Empty = new("", "", NodeResizeType.None);

    public static PointerId FromNode(NodeId nodeId) => new(nodeId.Value, "n", NodeResizeType.None);

    public static PointerId FromLine(LineId nodeId) => new(nodeId.Value, "l", NodeResizeType.None);

    public static PointerId FromLinePoint(LineId lineId, int pointIndex) =>
        new(lineId.Value, $"lp{pointIndex}", NodeResizeType.None, pointIndex);

    public static PointerId FromNodeResize(NodeId nodeId, NodeResizeType resizeType) =>
        new(nodeId.Value, ToSubId(resizeType), resizeType);

    public static PointerId Parse(string elementId)
    {
        var parts = elementId.Split('.');

        var id = parts[0];
        var subId = parts.Length > 1 ? parts[1] : "";
        return new(id, subId, ToNodeResizeType(subId), ToLinePointIndex(subId));
    }

    public bool IsNode => SubId == "n";
    public bool IsResize => NodeResizeType != NodeResizeType.None;
    public bool IsLine => SubId == "l";
    public bool IsLinePoint => LinePointIndex >= 0;

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
        if (!subId.StartsWith("lp"))
            return -1;

        return int.TryParse(subId[2..], out var index) ? index : -1;
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
