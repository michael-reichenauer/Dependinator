using Microsoft.CodeAnalysis;

namespace DependinatorCore.Parsing.Sources.Roslyn;

static class LinkParser
{
    public static Item Parse(ISymbol symbol, string sourceName)
    {
        var targetName = Names.GetFullName(symbol);
        var (nodeType, memberType) = NodeTypes.ToTypes(symbol);

        var link = new Link(
            sourceName,
            targetName,
            new LinkAttributes { TargetType = nodeType, MemberType = memberType }
        );
        return new Item(null, link);
    }
}
