using DependinatorCore.Parsing;
using DependinatorCore.Parsing.Sources.Roslyn;

namespace DependinatorCore.Tests.Parsing.Utils;

static class ItemsExtensions
{
    extension(IEnumerable<Item> items)
    {
        public IReadOnlyList<Node> Nodes() =>
            items.Where(i => i.Node is not null).Select(i => i.Node).Cast<Node>().ToList();

        public IReadOnlyList<Link> Links() =>
            items.Where(i => i.Link is not null).Select(i => i.Link).Cast<Link>().ToList();

        public Node Node<T>(string? memberName)
        {
            var typeName = Names.GetFullName<T>();
            if (memberName is not null)
            {
                var startMemberName = $"{typeName}.{memberName}";
                return items.Single(i => i.Node?.Name.StartsWith(startMemberName) ?? false).Node!;
            }

            return items.Single(i => i.Node?.Name == typeName).Node!;
        }

        public Link Link<TSource, TTarget>(string? sourceMemberName, string? targetMemberName)
        {
            var sourceTypeName = Names.GetFullName<TSource>();
            var targetTypeName = Names.GetFullName<TTarget>();

            if (sourceMemberName is not null && targetMemberName is not null)
            {
                var startSourceName = $"{sourceTypeName}.{sourceMemberName}";
                var startTargetName = $"{targetTypeName}.{targetMemberName}";
                return items
                    .Where(i => i.Link is not null)
                    .Single(i =>
                        i.Link!.Source.StartsWith(startSourceName) && i.Link!.Target.StartsWith(startTargetName)
                    )
                    .Link!;
            }
            else if (targetMemberName is not null)
            {
                var startTargetName = $"{targetTypeName}.{targetMemberName}";
                return items
                    .Where(i => i.Link is not null)
                    .Single(i => i.Link!.Source == sourceTypeName && i.Link!.Target.StartsWith(startTargetName))
                    .Link!;
            }
            else if (sourceMemberName is not null)
            {
                var startSourceName = $"{sourceTypeName}.{sourceMemberName}";
                return items
                    .Where(i => i.Link is not null)
                    .Single(i => i.Link!.Source.StartsWith(startSourceName) && i.Link!.Target == targetTypeName)
                    .Link!;
            }

            return items.Single(i => i.Link?.Source == sourceTypeName && i.Link?.Target == targetTypeName).Link!;
        }

        public IReadOnlyList<Link> LinksFrom<TSource>(string? sourceMemberName)
        {
            var sourceTypeName = Names.GetFullName<TSource>();
            if (sourceMemberName is not null)
            {
                var startMemberName = $"{sourceTypeName}.{sourceMemberName}";
                return items
                    .Where(i => i.Link?.Source.StartsWith(startMemberName) ?? false)
                    .Select(i => i.Link)
                    .Cast<Link>()
                    .ToList();
            }

            return items.Where(i => i.Link?.Source == sourceTypeName).Select(i => i.Link).Cast<Link>().ToList();
        }

        public IReadOnlyList<Link> LinksTo<TTarget>(string? targetMemberName)
        {
            var targetTypeName = Names.GetFullName<TTarget>();
            if (targetMemberName is not null)
            {
                var startMemberName = $"{targetTypeName}.{targetMemberName}";
                return items
                    .Where(i => i.Link?.Target.StartsWith(startMemberName) ?? false)
                    .Select(i => i.Link)
                    .Cast<Link>()
                    .ToList();
            }

            return items.Where(i => i.Link?.Target == targetTypeName).Select(i => i.Link).Cast<Link>().ToList();
        }
    }
}
