using Microsoft.CodeAnalysis;

namespace DependinatorRoslyn.Parsing;

static class SymbolUtils
{
    public static bool GetIsPrivate(ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Private;
    }
}
