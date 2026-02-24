using Microsoft.CodeAnalysis;

namespace DependinatorRoslyn.Parsing;

class MemberParser
{
    public static IEnumerable<Item> ParseTypeMember(ISymbol member, string fullTypeName, Compilation compilation)
    {
        var items = member switch
        {
            IMethodSymbol m => ParseMethod(m, fullTypeName, compilation),
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
            yield return new Item(null, LinkParser.Parse(memberNode.Node!.Name, fieldType));
    }

    static IEnumerable<Item> ParseProperty(IPropertySymbol member, string fullTypeName)
    {
        var memberNode = ParseMember(member, fullTypeName);
        yield return memberNode;

        // Handle property link
        if (member.Type is INamedTypeSymbol propertyType && !IgnoredTypes.IsIgnoredSystemType(propertyType))
            yield return new Item(null, LinkParser.Parse(memberNode.Node!.Name, propertyType));
    }

    static IEnumerable<Item> ParseMethod(IMethodSymbol member, string fullTypeName, Compilation compilation)
    {
        var memberNode = ParseMember(member, fullTypeName);
        yield return memberNode;

        var sentLinks = new HashSet<string>();
        foreach (var link in MethodLinkParser.ParseMethodLinks(member, memberNode.Node!.Name, compilation))
        {
            if (sentLinks.Add(link.Target))
                yield return new Item(null, link);
        }
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
