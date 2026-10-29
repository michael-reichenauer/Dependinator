using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    // The span in the file the user wrote. Razor components are C# generated from .razor
    // files, in syntax trees whose paths do not exist on disk: #line directives map the
    // @code members to their .razor lines, and the rest (the class itself, BuildRenderTree)
    // maps to the whole .razor file named by the generated file's #pragma checksum.
    public static FileLinePositionSpan GetSourceSpan(Location location)
    {
        var mapped = location.GetMappedLineSpan();
        if (mapped.HasMappedPath)
            return mapped;

        if (GetRazorFileSpan(location.SourceTree) is { } razorSpan)
            return razorSpan;

        return location.GetLineSpan();
    }

    static FileLinePositionSpan? GetRazorFileSpan(SyntaxTree? tree)
    {
        if (tree is null || File.Exists(tree.FilePath))
            return null;
        if (tree.GetRoot() is not CSharpSyntaxNode root)
            return null;
        if (root.GetFirstDirective() is not PragmaChecksumDirectiveTriviaSyntax checksum)
            return null;

        var path = checksum.File.ValueText;
        if (!path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            return null;

        var lastLine = Math.Max(0, File.ReadLines(path).Count() - 1);
        return new FileLinePositionSpan(path, new(0, 0), new(lastLine, 0));
    }

    static IEnumerable<FileLinePositionSpan> GetDeclarationSpans(ISymbol symbol)
    {
        foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is BaseTypeDeclarationSyntax typeDecl)
                yield return GetSourceSpan(typeDecl.GetLocation());
        }
    }

    static IEnumerable<FileLinePositionSpan> GetSourceLocationSpans(ISymbol symbol)
    {
        foreach (var location in symbol.Locations.Where(l => l.IsInSource))
        {
            yield return GetSourceSpan(location);
        }
    }
}
