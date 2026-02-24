using Microsoft.CodeAnalysis;

namespace DependinatorCore.Parsing.Sources.Roslyn;

static class SymbolUtils
{
    public static bool GetIsPrivate(ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Private;
    }
}
