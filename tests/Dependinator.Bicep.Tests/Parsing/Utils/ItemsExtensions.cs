using Dependinator.Core.Parsing;

namespace Dependinator.Bicep.Tests.Parsing.Utils;

static class ItemsExtensions
{
    extension(IReadOnlyList<Item> items)
    {
        public IEnumerable<Node> Nodes() => items.Where(i => i.Node is not null).Select(i => i.Node!);

        public IEnumerable<Link> Links() => items.Where(i => i.Link is not null).Select(i => i.Link!);

        public Node Node(string name) =>
            items.Nodes().FirstOrDefault(n => n.Name == name)
            ?? throw new Xunit.Sdk.XunitException(
                $"No node '{name}'. Nodes:\n  {string.Join("\n  ", items.Nodes().Select(n => n.Name))}"
            );

        public bool HasNode(string name) => items.Nodes().Any(n => n.Name == name);

        public bool HasLink(string source, string target) =>
            items.Links().Any(l => l.Source == source && l.Target == target);

        public IEnumerable<string> LinkTargetsOf(string source) =>
            items.Links().Where(l => l.Source == source).Select(l => l.Target);
    }
}
