using DependinatorCore.Parsing;

namespace Dependinator.Tests.Utils;

class ItemsMock : IItems
{
    readonly List<IItem> items = [];

    public int Count => items.Count;
    public int NodeCount => items.OfType<Node>().Count();
    public int LinkCount => items.OfType<Link>().Count();

    public IReadOnlyList<Link> Links => items.OfType<Link>().ToList();
    public IReadOnlyList<Node> Nodes => items.OfType<Node>().ToList();

    public Task SendAsync(IItem item)
    {
        items.Add(item);
        return Task.CompletedTask;
    }

    public Node GetNode(string nodeName) => items.OfType<Node>().Single(n => n.Name == nodeName);
}
