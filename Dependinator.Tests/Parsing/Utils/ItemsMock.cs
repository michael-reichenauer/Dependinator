using Dependinator.Parsing;

namespace Dependinator.Tests.Parsing.Utils;

class ItemsMock : IItems
{
    readonly List<IItem> items = [];

    public int Count => items.Count;
    public int NodeCount => items.OfType<Node>().Count();
    public int LinkCount => items.OfType<Node>().Count();

    public Task SendAsync(IItem item)
    {
        items.Add(item);
        return Task.CompletedTask;
    }

    public Node GetNode(Reference reference)
    {
        return items.OfType<Node>().Single(n => IsEqual(n.Name, reference));
    }

    public Link GetLink(Reference source, Reference target)
    {
        return items.OfType<Link>().Single(n => IsEqual(n.Source, source) && IsEqual(n.Target, target));
    }

    static bool IsEqual(string name, Reference reference)
    {
        if (reference.IsMember)
            return name.StartsWith(reference.Name);
        return name == reference.Name;
    }
}
