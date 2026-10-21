using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

static class LinkParser
{
    public static Link Parse(string sourceName, ISymbol symbol, bool isInheritance = false)
    {
        var declaration = GetDeclaration(symbol);
        var targetName = Names.GetFullName(declaration);
        var nodeType = NodeTypes.ToNodeType(declaration);

        return new Link(
            sourceName,
            targetName,
            new LinkProperties { TargetType = nodeType, IsInheritance = isInheritance ? true : null }
        );
    }

    // A use names a generic's construction (Result<int>, Result<int>.Error) and a call of an
    // extension method its reduced form, without the receiver (Classic(int)), while nodes are
    // named after declarations (Result<T>, Classic(Ns.Sample,int)).
    static ISymbol GetDeclaration(ISymbol symbol) =>
        symbol is IMethodSymbol { ReducedFrom: { } extensionMethod }
            ? extensionMethod.OriginalDefinition
            : symbol.OriginalDefinition;
}
