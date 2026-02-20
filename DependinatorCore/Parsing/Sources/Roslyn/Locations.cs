using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DependinatorCore.Parsing.Sources.Roslyn;

class Locations
{
    public static FileSpan GetFirstFileSpanOrNoValue(ISymbol symbol)
    {
        var spans = GetLocationSpans(symbol);
        if (!spans.Any())
            return NoValue.FileSpan;
        var firstSpan = spans.First();

        return new FileSpan(firstSpan.Path, firstSpan.StartLinePosition.Line, firstSpan.EndLinePosition.Line);
    }

    public static IEnumerable<FileLinePositionSpan> GetLocationSpans(ISymbol symbol)
    {
        var typeSpans = GetTypeDeclarationSpans(symbol);
        if (typeSpans.Any())
            return typeSpans;
        return GetTypeLocationSpans(symbol);
    }

    static IEnumerable<FileLinePositionSpan> GetTypeDeclarationSpans(ISymbol symbol)
    {
        foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
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

    static IEnumerable<FileLinePositionSpan> GetTypeLocationSpans(ISymbol symbol)
    {
        foreach (var location in symbol.Locations.Where(l => l.IsInSource))
        {
            yield return location.GetLineSpan();
        }
    }
}
