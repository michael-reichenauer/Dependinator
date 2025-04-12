namespace Dependinator.Diagrams;

record PointerId(string ElementId, string Id, string SubId)
{
    public static readonly PointerId Empty = new("", "", "");

    internal static PointerId Parse(string elementId)
    {
        var parts = elementId.Split('.');

        var id = parts[0];
        var subId = parts.Length > 1 ? parts[1] : "";
        return new(elementId, id, subId);
    }

    public bool IsResize =>
        SubId switch
        {
            "tl" => true,
            "tm" => true,
            "tr" => true,
            "ml" => true,
            "mr" => true,
            "bl" => true,
            "bm" => true,
            "br" => true,
            _ => false,
        };

    public bool IsIcon => SubId == "i";
    public bool IsContainer => SubId == "c";
    public bool IsNode => SubId == "c" || SubId == "i";
};
