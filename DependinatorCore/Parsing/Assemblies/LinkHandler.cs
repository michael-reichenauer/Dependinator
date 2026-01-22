using Mono.Cecil;

namespace DependinatorCore.Parsing.Assemblies;

internal class LinkHandler
{
    readonly IItems items;

    public LinkHandler(IItems items)
    {
        this.items = items;
    }

    public int LinksCount { get; private set; } = 0;

    public Task AddLinkAsync(string source, string target, NodeType targetType)
    {
        return SendLinkAsync(source, target, targetType);
    }

    public async Task AddLinkToTypeAsync(string sourceName, TypeReference targetType)
    {
        if (targetType is GenericInstanceType genericType)
        {
            await genericType.GenericArguments.ForEachAsync(argType => AddLinkToTypeAsync(sourceName, argType));
        }

        if (IsIgnoredReference(targetType))
            return;

        string targetNodeName = Name.GetTypeFullName(targetType);

        if (IsIgnoredTargetName(targetNodeName))
            return;

        await SendLinkAsync(sourceName, targetNodeName, NodeType.Type);
    }

    public async Task AddLinkToMemberAsync(string sourceName, IMemberDefinition memberInfo)
    {
        if (IsIgnoredTargetMember(memberInfo))
            return;

        string targetNodeName = Name.GetMemberFullName(memberInfo);

        if (IsIgnoredTargetName(targetNodeName))
            return;

        await SendLinkAsync(sourceName, targetNodeName, NodeType.Member);
    }

    private async Task SendLinkAsync(string sourceName, string targetName, NodeType targetType)
    {
        Link dataLink = new Link(sourceName, targetName, new() { TargetType = targetType });
        await items.SendAsync(dataLink);
        LinksCount++;
    }

    static bool IsIgnoredTargetMember(IMemberDefinition memberInfo)
    {
        return IgnoredTypes.IsIgnoredSystemType(memberInfo.DeclaringType)
            || IsGenericTypeArgument(memberInfo.DeclaringType);
    }

    static bool IsIgnoredTargetName(string targetNodeName)
    {
        return Name.IsCompilerGenerated(targetNodeName) || targetNodeName.StartsWith("mscorlib.");
    }

    static bool IsIgnoredReference(TypeReference targetType)
    {
        return targetType.FullName == "System.Void"
            || targetType.IsGenericParameter
            || IgnoredTypes.IsIgnoredSystemType(targetType)
            || IsGenericTypeArgument(targetType)
            || targetType is ByReferenceType refType && refType.ElementType.IsGenericParameter;
    }

    // Return true if type is a generic type parameter T, as in e.g. Get'T'(T value)s
    static bool IsGenericTypeArgument(MemberReference targetType)
    {
        return targetType.FullName == null && targetType.DeclaringType == null;
    }
}
