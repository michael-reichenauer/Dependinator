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
            var name = GetName<T>(memberName);

            return items.Nodes().Single(n => IsSame(n.Name, name));
        }

        public Link Link<TSource, TTarget>(string? sourceMemberName, string? targetMemberName)
        {
            var sourceName = GetName<TSource>(sourceMemberName);
            var targetName = GetName<TTarget>(targetMemberName);

            return items.Links().Single(l => IsSame(l.Source, sourceName) && IsSame(l.Target, targetName));
        }

        public IReadOnlyList<Link> LinksFrom<TSource>(string? sourceMemberName)
        {
            var sourceName = GetName<TSource>(sourceMemberName);

            return items.Links().Where(l => IsSame(l.Source, sourceName)).ToList();
        }

        public IReadOnlyList<Link> LinksTo<TTarget>(string? targetMemberName)
        {
            var targetName = GetName<TTarget>(targetMemberName);

            return items.Links().Where(i => IsSame(i.Target, targetName)).ToList();
        }

        static string GetName<T>(string? memberName)
        {
            var typeName = Names.GetFullName<T>();

            if (memberName is not null)
            {
                // if member is a method, we add a '(' to make IsSame() below possible to handle method names
                var methods = typeof(T)
                    .GetMethods(
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.Static
                            | System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.DeclaredOnly
                    )
                    .Where(m => !m.IsSpecialName);
                if (methods.Any(m => m.Name == memberName))
                    memberName += '(';

                return $"{typeName}.{memberName}";
            }

            return typeName;
        }

        // For method names, the nodeName contains method parameters as well.
        // Since name (if method) does not contain the parameters, we need to use StartWith
        static bool IsSame(string nodeName, string name)
        {
            var nameIndex = name.FindIndexBy(c => c is '(');
            if (nameIndex != -1)
                return nodeName.StartsWith(name); // the '(') was added by GetName<T>() above

            return nodeName == name;
        }
    }
}
