using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

static class SymbolUtils
{
    public static bool GetIsPrivate(ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Private;
    }
}
