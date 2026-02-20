using DependinatorCore.Parsing;
using DependinatorCore.Parsing.Sources.Roslyn;

namespace DependinatorCore.Tests.Parsing.Utils;

static class ItemsExtensions
{
    extension(List<Item> items)
    {
        public Node Node<T>()
        {
            var typeName = Names.GetFullName<T>();
            return items.Single(i => i.Node?.Name == typeName).Node!;
        }

        public Node Node<T>(string memberName)
        {
            var startMemberName = $"{Names.GetFullName<T>()}.{memberName}";
            return items.Single(i => i.Node?.Name.StartsWith(startMemberName) ?? false).Node!;
        }

        public IReadOnlyList<Node> Nodes() =>
            items.Where(i => i.Node is not null).Select(i => i.Node).Cast<Node>().ToList();

        public Link Link<TSource, TTarget>(string? sourceMemberName, string? targetMemberName)
        {
            var sourceTypeName = Names.GetFullName<TSource>();
            var targetTypeName = Names.GetFullName<TTarget>();

            if (sourceMemberName is null && targetMemberName is null)
            {
                return items.Single(i => i.Link?.Source == sourceTypeName && i.Link?.Target == targetTypeName).Link!;
            }
            else if (sourceMemberName is null)
            {
                var startTargetName = $"{targetTypeName}.{targetMemberName}";
                return items
                    .Where(i => i.Link is not null)
                    .Single(i => i.Link!.Source == sourceTypeName && i.Link!.Target.StartsWith(startTargetName))
                    .Link!;
            }
            else if (targetMemberName is null)
            {
                var startSourceName = $"{sourceTypeName}.{sourceMemberName}";
                return items
                    .Where(i => i.Link is not null)
                    .Single(i => i.Link!.Source.StartsWith(startSourceName) && i.Link!.Target == targetTypeName)
                    .Link!;
            }
            else
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
        }

        public IReadOnlyList<Link> Links() =>
            items.Where(i => i.Link is not null).Select(i => i.Link).Cast<Link>().ToList();

        public IReadOnlyList<Link> LinksFrom<TSource>()
        {
            var sourceName = Names.GetFullName<TSource>();
            return items.Where(i => i.Link?.Source == sourceName).Select(i => i.Link).Cast<Link>().ToList();
        }

        public IReadOnlyList<Link> LinksFrom<TSource>(string sourceMemberName)
        {
            var sourceTypeName = Names.GetFullName<TSource>();
            var startMemberName = $"{sourceTypeName}.{sourceMemberName}";
            return items
                .Where(i => i.Link?.Source.StartsWith(startMemberName) ?? false)
                .Select(i => i.Link)
                .Cast<Link>()
                .ToList();
        }

        public IReadOnlyList<Link> LinksTo<TTarget>()
        {
            var targetName = Names.GetFullName<TTarget>();
            return items.Where(i => i.Link?.Target == targetName).Select(i => i.Link).Cast<Link>().ToList();
        }

        public IReadOnlyList<Link> LinksTo<TTarget>(string targetMemberName)
        {
            var targetTypeName = Names.GetFullName<TTarget>();
            var startMemberName = $"{targetTypeName}.{targetMemberName}";
            return items
                .Where(i => i.Link?.Target.StartsWith(startMemberName) ?? false)
                .Select(i => i.Link)
                .Cast<Link>()
                .ToList();
        }
    }
}
