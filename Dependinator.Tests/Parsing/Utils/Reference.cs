using System.Reflection;

namespace Dependinator.Tests.Parsing.Utils;

class Reference
{
    public string Name { get; init; } = "";
    public bool IsMember { get; init; }

    public static Reference Ref<T>(string? memberName = null)
    {
        return new Reference() { Name = NodeName<T>(memberName), IsMember = !string.IsNullOrEmpty(memberName) };
    }

    internal static object From(Action firstFunction)
    {
        throw new NotImplementedException();
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
