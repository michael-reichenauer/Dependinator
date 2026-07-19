using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

static class LinkParser
{
    public static Link Parse(string sourceName, ISymbol symbol, bool isInheritance = false)
    {
        var targetName = Names.GetFullName(symbol);
        var nodeType = NodeTypes.ToNodeType(symbol);

        return new Link(
            sourceName,
            targetName,
            new LinkProperties { TargetType = nodeType, IsInheritance = isInheritance ? true : null }
        );
    }
}
