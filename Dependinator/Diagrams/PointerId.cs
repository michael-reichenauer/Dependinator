namespace Dependinator.Diagrams;

record PointerId(string Id, string SubId)
{
    public static readonly PointerId Empty = new("", "");

    internal static PointerId Parse(string targetId)
    {
        var parts = targetId.Split('.');

        var id = parts[0];
        var subId = parts.Length > 1 ? parts[1] : "";
        return new(id, subId);
    }
}
