using Microsoft.CodeAnalysis;

namespace DependinatorCore.Parsing.Sources.Roslyn;

class MemberParser
{
    public static IEnumerable<Item> ParseTypeMember(ISymbol member, string fullTypeName)
    {
        var items = member switch
        {
            IMethodSymbol m => ParseMethod(m, fullTypeName),
            IPropertySymbol p => ParseProperty(p, fullTypeName),
            IFieldSymbol f => ParseField(f, fullTypeName),
            IEventSymbol e => ParseEvent(e, fullTypeName),
            _ => throw new NotSupportedException($"Member type not supported: {member}"),
        };

        foreach (var item in items)
            yield return item;
    }

    static IEnumerable<Item> ParseEvent(IEventSymbol member, string fullTypeName)
    {
        yield return ParseMember(member, fullTypeName);
    }

    static IEnumerable<Item> ParseField(IFieldSymbol member, string fullTypeName)
    {
        var memberNode = ParseMember(member, fullTypeName);
        yield return memberNode;

        // Handle field link
        if (member.Type is INamedTypeSymbol fieldType && !IgnoredTypes.IsIgnoredSystemType(fieldType))
            yield return LinkParser.Parse(fieldType, memberNode.Node!.Name);
    }

    static IEnumerable<Item> ParseProperty(IPropertySymbol member, string fullTypeName)
    {
        var memberNode = ParseMember(member, fullTypeName);
        yield return memberNode;

        // Handle property link
        if (member.Type is INamedTypeSymbol fieldType && !IgnoredTypes.IsIgnoredSystemType(fieldType))
            yield return LinkParser.Parse(fieldType, memberNode.Node!.Name);
    }

    static IEnumerable<Item> ParseMethod(IMethodSymbol member, string fullTypeName)
    {
        var memberNode = ParseMember(member, fullTypeName);
        yield return ParseMember(member, fullTypeName);

        // Get links for method parameters
        foreach (var parameter in member.Parameters)
        {
            if (parameter.Type is not INamedTypeSymbol parameterType)
                continue;
            if (IgnoredTypes.IsIgnoredSystemType(parameterType))
                continue;

            yield return LinkParser.Parse(parameterType, memberNode.Node!.Name);
        }

        // Parse links to method body fields types and references to other types and methods
    }

    static Item ParseMember(ISymbol member, string fullTypeName)
    {
        var name = Names.GetFullMemberName(member, fullTypeName);
        var fileSpan = Locations.GetFirstFileSpanOrNoValue(member);
        var leadingComment = CommentExtractor.GetLeadingCommentOrNoValue(member, fileSpan);
        var nodeType = NodeTypes.ToTypes(member);
        var isPrivate = SymbolUtils.GetIsPrivate(member);

        return new Item(
            new Node(
                name,
                new NodeProperties
                {
                    Type = nodeType,
                    Description = leadingComment,
                    FileSpan = fileSpan,
                    IsPrivate = isPrivate,
                }
            ),
            null
        );
    }
}

internal class IgnoredTypes
{
    public static bool IsIgnoredSystemType(INamedTypeSymbol type)
    {
        if (type.ContainingModule.Name == "System.Runtime.dll")
            return true;

        return false;
        // if (type.FullName.StartsWith("__Blazor"))
        //     return true;

        // return IsSystemIgnoredModuleName(targetType.Scope.Name);
    }

    public static bool IsSystemIgnoredModuleName(string moduleName)
    {
        return moduleName == "mscorlib"
            || moduleName == "PresentationFramework"
            || moduleName == "PresentationCore"
            || moduleName == "WindowsBase"
            || moduleName == "System"
            || moduleName.StartsWith("Microsoft.")
            || moduleName.StartsWith("System.");
    }
}
