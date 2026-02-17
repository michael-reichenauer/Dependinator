using System.Collections.Concurrent;
using DependinatorCore.Parsing;
using DependinatorCore.Parsing.Utils;

namespace DependinatorCore.Tests.Parsing.Utils;

class ItemsMock : IItems
{
    public int Count => Nodes.Count + Links.Count;

    public ConcurrentBag<Node> Nodes { get; } = [];
    public ConcurrentBag<Link> Links { get; } = [];

    public Task SendAsync(Node node)
    {
        Nodes.Add(node);
        return Task.CompletedTask;
    }

    public Task SendAsync(Link link)
    {
        Links.Add(link);
        return Task.CompletedTask;
    }

    public Node GetNode(string nodeName) => Nodes.Single(n => n.Name == nodeName);

    public Node GetNode(Reference reference) => Nodes.Single(n => IsEqual(n.Name, reference));

    public Link GetLink(Reference source, Reference target) =>
        Links.Single(n => IsEqual(n.Source, source) && IsEqual(n.Target, target));

    public IReadOnlyList<Link> GetLinksFrom(Reference source) => Links.Where(n => IsEqual(n.Source, source)).ToList();

    public IReadOnlyList<Link> GetLinksTo(Reference target) => Links.Where(n => IsEqual(n.Target, target)).ToList();

    static bool IsEqual(string name, Reference reference)
    {
        if (reference.IsMember)
            return name.StartsWith(reference.Name);
        return name == reference.Name;
    }
}
