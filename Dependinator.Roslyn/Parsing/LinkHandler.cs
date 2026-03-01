using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

static class LinkParser
{
    public static Link Parse(string sourceName, ISymbol symbol)
    {
        var targetName = Names.GetFullName(symbol);
        var nodeType = NodeTypes.ToTypes(symbol);

        return new Link(sourceName, targetName, new LinkProperties { TargetType = nodeType });
    }
}
