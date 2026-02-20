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
        yield return ParseMember(member, fullTypeName);
    }

    static IEnumerable<Item> ParseProperty(IPropertySymbol member, string fullTypeName)
    {
        yield return ParseMember(member, fullTypeName);
    }

    static IEnumerable<Item> ParseMethod(IMethodSymbol member, string fullTypeName)
    {
        yield return ParseMember(member, fullTypeName);
    }

    static Item ParseMember(ISymbol member, string fullTypeName)
    {
        var name = Names.GetFullMemberName(member, fullTypeName);
        var fileSpan = Locations.GetFirstFileSpanOrNoValue(member);
        var leadingComment = CommentExtractor.GetLeadingCommentOrNoValue(member, fileSpan);
        var memberType = ToMemberType(member);

        return new Item(
            new Node(
                name,
                new NodeAttributes
                {
                    Type = NodeType.Member,
                    MemberType = memberType,
                    Description = leadingComment,
                    FileSpan = fileSpan,
                }
            ),
            null
        );
    }

    static MemberType ToMemberType(ISymbol member) =>
        member switch
        {
            IMethodSymbol => MemberType.Method,
            IPropertySymbol => MemberType.Property,
            IFieldSymbol => MemberType.Field,
            IEventSymbol => MemberType.Event,
            _ => MemberType.None,
        };
}
