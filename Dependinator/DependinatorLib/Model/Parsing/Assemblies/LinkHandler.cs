using Mono.Cecil;

namespace Dependinator.Model.Parsing.Assemblies;

internal class LinkHandler
{
    readonly Action<Link> linkCallback;


    public LinkHandler(Action<Link> linkCallback)
    {
        this.linkCallback = linkCallback;
    }


    public int LinksCount { get; private set; } = 0;


    public void AddLink(string source, string target, string targetType)
    {
        SendLink(source, target, targetType);
    }


    public void AddLinkToType(string sourceName, TypeReference targetType)
    {
        if (targetType is GenericInstanceType genericType)
        {
            genericType.GenericArguments.ForEach(argType => AddLinkToType(sourceName, argType));
        }

        if (IsIgnoredReference(targetType))
        {
            return;
        }

        string targetNodeName = Name.GetTypeFullName(targetType);

        if (IsIgnoredTargetName(targetNodeName))
        {
            return;
        }

        SendLink(sourceName, targetNodeName, NodeType.TypeType);
    }


    public void AddLinkToMember(string sourceName, IMemberDefinition memberInfo)
    {
        if (IsIgnoredTargetMember(memberInfo))
        {
            return;
        }

        string targetNodeName = Name.GetMemberFullName(memberInfo);

        if (IsIgnoredTargetName(targetNodeName))
        {
            return;
        }

        SendLink(sourceName, targetNodeName, NodeType.MemberType);
    }


    private void SendLink(string source, string targetName, string targetType)
    {
        Link dataLink = new Link(source, targetName, targetType);
        linkCallback(dataLink);
        LinksCount++;
    }


    static bool IsIgnoredTargetMember(IMemberDefinition memberInfo)
    {
        return IgnoredTypes.IsIgnoredSystemType(memberInfo.DeclaringType)
               || IsGenericTypeArgument(memberInfo.DeclaringType);
    }


    static bool IsIgnoredTargetName(string targetNodeName)
    {
        return Name.IsCompilerGenerated(targetNodeName) ||
               targetNodeName.StartsWith("mscorlib.");
    }


    static bool IsIgnoredReference(TypeReference targetType)
    {
        return targetType.FullName == "System.Void"
               || targetType.IsGenericParameter
               || IgnoredTypes.IsIgnoredSystemType(targetType)
               || IsGenericTypeArgument(targetType)
               || targetType is ByReferenceType refType && refType.ElementType.IsGenericParameter;
    }


    /// <summary>
    /// Return true if type is a generic type parameter T, as in e.g. Get'T'(T value)
    /// </summary>
    static bool IsGenericTypeArgument(MemberReference targetType)
    {
        return
            targetType.FullName == null
            && targetType.DeclaringType == null;
    }
}

