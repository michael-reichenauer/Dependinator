using Mono.Cecil;

namespace Dependinator.Core.Tests.Parsing.Utils;

class Reference
{
    public string Name { get; init; } = "";
    public bool IsMember { get; init; }

    public static Reference Ref<T>(string? memberName = null)
    {
        return new Reference() { Name = NodeName<T>(memberName), IsMember = !string.IsNullOrEmpty(memberName) };
    }

    public static Reference RefSubType<T>(string memberName)
    {
        return new Reference() { Name = NodeName<T>(memberName), IsMember = false };
    }

    internal static object From(Action firstFunction)
    {
        throw new NotImplementedException();
    }

    public static string NodeName<T>(string? memberName = null)
    {
        var typeDefinition = AssemblyHelper.GetTypeDefinition<T>();
        if (memberName is null)
        {
            return Dependinator.Core.Parsing.Assemblies.Name.GetTypeFullName(typeDefinition);
        }
        if (TryGetMember(typeDefinition, memberName, out var member))
        {
            return Dependinator.Core.Parsing.Assemblies.Name.GetMemberFullName(member);
        }

        throw new Exception($"Invalid reference '{typeof(T).FullName}', '{memberName}'");
    }

    static bool TryGetMember(TypeDefinition typeDefinition, string memberName, out IMemberDefinition member)
    {
        if (TryGetMethod(typeDefinition, memberName, out member))
            return true;

        if (TryGetProperty(typeDefinition, memberName, out member))
            return true;

        if (TryGetField(typeDefinition, memberName, out member))
            return true;

        if (TryGetNestedType(typeDefinition, memberName, out member))
            return true;

        member = default!;
        return false;
    }

    static bool TryGetMethod(TypeDefinition type, string memberName, out IMemberDefinition method)
    {
        method = type.Methods.FirstOrDefault(m => m.Name == memberName)!;
        return method != null;
    }

    static bool TryGetProperty(TypeDefinition type, string memberName, out IMemberDefinition property)
    {
        property = type.Properties.FirstOrDefault(m => m.Name == memberName)!;
        return property != null;
    }

    static bool TryGetField(TypeDefinition type, string memberName, out IMemberDefinition field)
    {
        field = type.Fields.FirstOrDefault(m => m.Name == memberName)!;
        return field != null;
    }

    static bool TryGetNestedType(TypeDefinition type, string memberName, out IMemberDefinition nestedType)
    {
        nestedType = type.NestedTypes.FirstOrDefault(m => m.Name == memberName)!;
        return nestedType != null;
    }
}
