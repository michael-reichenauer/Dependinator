using System.Reflection;
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

    public Node GetNode<T>()
    {
        var nodeName = NodeName<T>();
        return items.OfType<Node>().Single(n => n.Name == nodeName);
    }

    public Node GetNode<T>(string memberName)
    {
        var nodeName = NodeName<T>(memberName);
        return items.OfType<Node>().Single(n => n.Name.StartsWith(nodeName));
    }

    public Link GetLink<TSource, TTarget>() => GetLinkImlp<TSource, TTarget>(null, null);

    public Link GetLink<TSource, TTarget>(string sourceMemberName, string targetMemberName) =>
        GetLinkImlp<TSource, TTarget>(sourceMemberName, targetMemberName);

    Link GetLinkImlp<TSource, TTarget>(string? sourceMemberName, string? targetMemberName)
    {
        var sourceNodeName = NodeName<TSource>(sourceMemberName);
        var targetNodeName = NodeName<TTarget>(targetMemberName);
        return items
            .OfType<Link>()
            .Single(n => n.Source.StartsWith(sourceNodeName) && n.Target.StartsWith(targetNodeName));
    }

    static string NodeName<T>(string? memberName = null)
    {
        if (memberName is not null)
        {
            var allMembers = typeof(T).GetMembers(
                BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.FlattenHierarchy
            );
            var member = allMembers.Single(m => m.Name == memberName);
            if (member.MemberType == MemberTypes.Method)
                memberName = $"{memberName}(";
            else if (member.MemberType == MemberTypes.Constructor)
                memberName = $"{memberName[1..]}(";
        }

        var typeName = typeof(T).FullName;
        var moduleName = Path.GetFileNameWithoutExtension(typeof(T).Module.Name).Replace(".", "*");
        return string.IsNullOrEmpty(memberName) ? $"{moduleName}.{typeName}" : $"{moduleName}.{typeName}.{memberName}";
    }
}
