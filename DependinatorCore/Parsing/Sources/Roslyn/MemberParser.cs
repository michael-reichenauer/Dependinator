using Microsoft.CodeAnalysis;

namespace DependinatorCore.Parsing.Sources;

class MemberParser
{
    public static IEnumerable<Parsing.Item> ParseTypeMember(ISymbol member, string fullTypeName)
    {
        if (member is INamedTypeSymbol)
            yield break;

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

    static IEnumerable<Parsing.Item> ParseEvent(IEventSymbol member, string fullTypeName)
    {
        yield return ParseMember(member, fullTypeName);
    }

    static IEnumerable<Parsing.Item> ParseField(IFieldSymbol member, string fullTypeName)
    {
        yield return ParseMember(member, fullTypeName);
    }

    static IEnumerable<Parsing.Item> ParseProperty(IPropertySymbol member, string fullTypeName)
    {
        yield return ParseMember(member, fullTypeName);
    }

    static IEnumerable<Parsing.Item> ParseMethod(IMethodSymbol member, string fullTypeName)
    {
        yield return ParseMember(member, fullTypeName);
    }

    static Parsing.Item ParseMember(ISymbol member, string fullTypeName)
    {
        var name = Names.GetFullMemberName(member, fullTypeName);
        var fileSpan = Locations.GetFirstFileSpanOrNoValue(member);
        var leadingComment = CommentExtractor.GetCommentOrNoValue(member, fileSpan);
        var memberType = ToMemberType(member);

        return new Parsing.Item(
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
