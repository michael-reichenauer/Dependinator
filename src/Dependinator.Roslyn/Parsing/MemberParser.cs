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
                    Signature = Signature(member) ?? NoValue.String,
                }
            ),
            null
        );
    }

    // The type names as written in code, without namespaces: "Task<string>", "int?".
    static readonly SymbolDisplayFormat SignatureTypeFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
    );

    // What a member row shows after its name, UML style: "(string question): Task<int>" for a
    // method (no return type when void or a constructor), ": int" for a field, property or event.
    internal static string? Signature(ISymbol member) =>
        member switch
        {
            IMethodSymbol method => $"({Parameters(method.Parameters)})"
                + (
                    method.ReturnsVoid || method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor
                        ? ""
                        : $": {TypeText(method.ReturnType)}"
                ),
            IPropertySymbol { IsIndexer: true } indexer =>
                $"[{Parameters(indexer.Parameters)}]: {TypeText(indexer.Type)}",
            IPropertySymbol property => $": {TypeText(property.Type)}",
            IFieldSymbol field => $": {TypeText(field.Type)}",
            IEventSymbol @event => $": {TypeText(@event.Type)}",
            _ => null,
        };

    static string Parameters(IEnumerable<IParameterSymbol> parameters) =>
        string.Join(", ", parameters.Select(p => $"{TypeText(p.Type)} {p.Name}"));

    static string TypeText(ITypeSymbol type) => type.ToDisplayString(SignatureTypeFormat);

    static bool IsSameAsContainingType(INamedTypeSymbol type, ISymbol member)
    {
        // A generic type's own construction (Result<int> in Result<T>) is the same type too
        return SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, member.ContainingType);
    }
}
