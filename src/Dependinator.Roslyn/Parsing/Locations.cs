using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dependinator.Roslyn.Parsing;

static class Locations
{
    public static FileSpan GetFirstFileSpanOrNoValue(ISymbol symbol)
    {
        foreach (var span in GetLocationSpans(symbol))
            return ToFileSpan(span);

        return NoValue.FileSpan;
    }

    public static IEnumerable<FileLinePositionSpan> GetLocationSpans(ISymbol symbol)
    {
        // Prefer full type declaration spans (covering the whole declaration); fall back to
        // the symbol's source locations (just the identifier) for members and other symbols.
        var declarationSpans = GetDeclarationSpans(symbol).ToList();
        if (declarationSpans.Count > 0)
            return declarationSpans;
        return GetSourceLocationSpans(symbol);
    }

    public static FileSpan ToFileSpan(FileLinePositionSpan span)
    {
        return new FileSpan(span.Path, span.StartLinePosition.Line, span.EndLinePosition.Line);
    }

    static IEnumerable<FileLinePositionSpan> GetDeclarationSpans(ISymbol symbol)
    {
        foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is BaseTypeDeclarationSyntax typeDecl)
                yield return typeDecl.GetLocation().GetLineSpan();
        }
    }

    static IEnumerable<FileLinePositionSpan> GetSourceLocationSpans(ISymbol symbol)
    {
        foreach (var location in symbol.Locations.Where(l => l.IsInSource))
        {
            yield return location.GetLineSpan();
        }
    }
}
