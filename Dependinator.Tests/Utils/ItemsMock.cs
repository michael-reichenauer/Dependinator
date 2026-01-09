using System.Collections.Concurrent;
using DependinatorCore.Parsing;

namespace Dependinator.Tests.Utils;

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
}
