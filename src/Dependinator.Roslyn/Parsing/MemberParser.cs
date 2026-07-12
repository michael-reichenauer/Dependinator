using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

static class MemberParser
{
    public static IEnumerable<Item> ParseTypeMember(ISymbol member, string fullTypeName, Compilation compilation)
    {
        switch (member)
        {
            case IMethodSymbol m:
                return ParseMethod(m, fullTypeName, compilation);
            case IPropertySymbol p:
                return ParseProperty(p, fullTypeName, compilation);
            case IFieldSymbol f:
                return ParseField(f, fullTypeName);
            case IEventSymbol e:
                return ParseEvent(e, fullTypeName);
            default:
                // Skip unknown member kinds rather than failing the whole parse
                Log.Warn($"Unsupported member kind {member.Kind}: {member}");
                return [];
        }
    }

    static IEnumerable<Item> ParseEvent(IEventSymbol member, string fullTypeName)
    {
        yield return ParseMember(member, fullTypeName);
    }

    static IEnumerable<Item> ParseField(IFieldSymbol member, string fullTypeName)
    {
        var memberNode = ParseMember(member, fullTypeName);
        yield return memberNode;

        // Link to the field's type
        if (
            member.Type is INamedTypeSymbol fieldType
            && !IsSameAsContainingType(fieldType, member)
            && !IgnoredTypes.IsIgnored(fieldType)
        )
            yield return new Item(null, LinkParser.Parse(memberNode.Node!.Name, fieldType));
    }

    static IEnumerable<Item> ParseProperty(IPropertySymbol member, string fullTypeName, Compilation compilation)
    {
        var memberNode = ParseMember(member, fullTypeName);
        yield return memberNode;

        var sentLinks = new HashSet<string>();

        // Link to the property's type
        if (
            member.Type is INamedTypeSymbol propertyType
            && !IsSameAsContainingType(propertyType, member)
            && !IgnoredTypes.IsIgnored(propertyType)
        )
        {
            var typeLink = LinkParser.Parse(memberNode.Node!.Name, propertyType);
            if (sentLinks.Add(typeLink.Target))
                yield return new Item(null, typeLink);
        }

        // Links from the property accessor bodies
        foreach (var link in MethodLinkParser.ParsePropertyLinks(member, memberNode.Node!.Name, compilation))
        {
            if (sentLinks.Add(link.Target))
                yield return new Item(null, link);
        }
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
        var nodeType = NodeTypes.ToNodeType(member);
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

    static bool IsSameAsContainingType(INamedTypeSymbol type, ISymbol member)
    {
        return SymbolEqualityComparer.Default.Equals(type, member.ContainingType);
    }
}
