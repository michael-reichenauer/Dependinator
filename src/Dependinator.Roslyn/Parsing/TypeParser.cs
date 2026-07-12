using Dependinator.Core.Parsing.Utils;
using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

static class TypeParser
{
    public static IEnumerable<Item> ParseType(INamedTypeSymbol type, Compilation compilation, string moduleName)
    {
        var (fullTypeName, typeName) = Names.GetFullTypeNameAndTypeName(type, moduleName);

        if (IgnoredTypes.IsIgnored(type, typeName))
            yield break;

        var fileSpan = Locations.GetFirstFileSpanOrNoValue(type);
        var leadingComment = CommentExtractor.GetLeadingCommentOrNoValue(type, fileSpan);
        var isPrivate = SymbolUtils.GetIsPrivate(type);

        var description = leadingComment;
        IReadOnlyList<(string Target, string Text)> lineDescriptions = [];
        if (leadingComment is not null && leadingComment != NoValue.String)
        {
            (var nodeDescription, lineDescriptions) = CommentDescriptions.Parse(leadingComment);
            // An arrow-only comment has no node description; NoValue.String clears any previous one
            description = nodeDescription ?? NoValue.String;
        }

        yield return new Item(
            new Node(
                fullTypeName,
                new NodeProperties
                {
                    Type = SymbolUtils.GetTypeNodeType(type),
                    Description = description,
                    FileSpan = fileSpan,
                    IsPrivate = isPrivate,
                }
            ),
            null
        );

        foreach (var (target, text) in lineDescriptions)
            yield return new Item(null, null, new LineDescription(fullTypeName, target, text));

        foreach (var item in ParseTypeLinks(type, fullTypeName))
            yield return item;

        foreach (var item in ParseTypeMembers(type, fullTypeName, compilation))
            yield return item;
    }

    internal static IEnumerable<Item> ParseTypeLinks(INamedTypeSymbol type, string fullTypeName)
    {
        if (
            type.BaseType is { } baseType
            && baseType.SpecialType != SpecialType.System_Object
            && !IgnoredTypes.IsIgnored(baseType)
        )
            yield return new Item(null, LinkParser.Parse(fullTypeName, baseType));

        foreach (var interfaceType in type.Interfaces.Where(it => !IgnoredTypes.IsIgnored(it)))
            yield return new Item(null, LinkParser.Parse(fullTypeName, interfaceType));
    }

    internal static IEnumerable<Item> ParseTypeMembers(
        INamedTypeSymbol type,
        string fullTypeName,
        Compilation compilation
    )
    {
        foreach (ISymbol member in GetMemberSymbols(type))
        {
            if (!SymbolEqualityComparer.Default.Equals(member.ContainingType, type))
                continue;
            if (member is INamedTypeSymbol)
                continue; // Nested type, handled by GetAllNamedTypes in SourceParser

            foreach (var item in MemberParser.ParseTypeMember(member, fullTypeName, compilation))
                yield return item;
        }
    }

    internal static IEnumerable<ISymbol> GetMemberSymbols(INamedTypeSymbol type)
    {
        return type.GetMembers().Where(m => !m.IsImplicitlyDeclared);
    }
}
