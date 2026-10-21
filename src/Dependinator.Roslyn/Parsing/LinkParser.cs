using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

static class LinkParser
{
    public static Link Parse(string sourceName, ISymbol symbol, bool isInheritance = false)
    {
        ISymbol declaration = GetDeclaration(symbol);
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
        symbol switch
        {
            IMethodSymbol { ReducedFrom: { } extensionMethod } => extensionMethod.OriginalDefinition,
            IMethodSymbol method when GetExtensionBlockMember(method) is { } member => member,
            _ => symbol.OriginalDefinition,
        };

    // A call of a C# extension block member in its static form (Ext.Twice(t)) names the method the
    // compiler implements the member with, in the static class beside the block, and no node has
    // its name: the declaration is the block's member (for an accessor, its property).
    static ISymbol? GetExtensionBlockMember(IMethodSymbol method)
    {
        if (!method.IsStatic || method.ContainingType is not { IsStatic: true } type)
            return null;

        IMethodSymbol implementation = method.OriginalDefinition;
        return type.GetTypeMembers()
            .Where(t => t.IsExtension)
            .SelectMany(block => block.GetMembers().OfType<IMethodSymbol>())
            .Where(member =>
                SymbolEqualityComparer.Default.Equals(member.AssociatedExtensionImplementation, implementation)
            )
            .Select(member => member.AssociatedSymbol ?? member)
            .FirstOrDefault();
    }
}
