using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DependinatorCore.Parsing.Sources;

class Locations
{
    public static IEnumerable<FileLinePositionSpan> GetLocationSpans(INamedTypeSymbol typeSymbol)
    {
        var typeSpans = GetTypeDeclarationSpans(typeSymbol);
        if (typeSpans.Any())
            return typeSpans;
        return GetTypeLocationSpans(typeSymbol);
    }

    static IEnumerable<FileLinePositionSpan> GetTypeDeclarationSpans(INamedTypeSymbol typeSymbol)
    {
        foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();
            if (syntax is TypeDeclarationSyntax typeDecl)
            {
                yield return typeDecl.GetLocation().GetLineSpan();
            }
            else if (syntax is BaseTypeDeclarationSyntax baseTypeDecl)
            {
                yield return baseTypeDecl.GetLocation().GetLineSpan();
            }
        }
    }

    static IEnumerable<FileLinePositionSpan> GetTypeLocationSpans(INamedTypeSymbol typeSymbol)
    {
        foreach (var location in typeSymbol.Locations.Where(l => l.IsInSource))
        {
            yield return location.GetLineSpan();
        }
    }
}
