#if !BROWSER_WASM
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
#endif

namespace DependinatorCore.Parsing.Sources;

#if !BROWSER_WASM
static class TypeCommentExtractor
{
    public static string? GetTypeComment(INamedTypeSymbol typeSymbol, FileLinePositionSpan declarationSpan)
    {
        foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is not BaseTypeDeclarationSyntax typeDecl)
                continue;

            var lineSpan = typeDecl.GetLocation().GetLineSpan();
            if (
                lineSpan.Path != declarationSpan.Path
                || lineSpan.StartLinePosition.Line != declarationSpan.StartLinePosition.Line
            )
                continue;

            return GetLeadingComment(typeDecl);
        }

        return null;
    }

    static string? GetLeadingComment(BaseTypeDeclarationSyntax typeDecl)
    {
        var leadingTrivia = typeDecl.GetLeadingTrivia();
        List<string> commentLines = [];
        var isInsideCommentBlock = false;
        var endOfLineCount = 0;

        for (var i = leadingTrivia.Count - 1; i >= 0; i--)
        {
            var trivia = leadingTrivia[i];
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                continue;

            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                if (!isInsideCommentBlock)
                    continue;

                endOfLineCount++;
                if (endOfLineCount > 1)
                    break;
                continue;
            }

            if (IsCommentTrivia(trivia))
            {
                isInsideCommentBlock = true;
                endOfLineCount = 0;
                commentLines.InsertRange(0, NormalizeCommentLines(trivia));
                continue;
            }

            break;
        }

        if (!commentLines.Any())
            return null;

        var comment = string.Join(Environment.NewLine, commentLines);
        return string.IsNullOrWhiteSpace(comment) ? null : comment;
    }

    static bool IsCommentTrivia(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
            || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
            || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
            || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
    }

    static IEnumerable<string> NormalizeCommentLines(SyntaxTrivia commentTrivia)
    {
        var text = commentTrivia.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
            return [];

        if (text.StartsWith("///"))
            return text.Split('\n')
                .Select(line => line.Replace("\r", "").Trim())
                .Select(line => line.TrimPrefix("///").Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

        if (text.StartsWith("//"))
            return text.Split('\n')
                .Select(line => line.Replace("\r", "").Trim())
                .Select(line => line.TrimPrefix("//").Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

        if (text.StartsWith("/*"))
            return text.TrimPrefix("/*")
                .TrimSuffix("*/")
                .Split('\n')
                .Select(line => line.Replace("\r", "").Trim())
                .Select(line => line.TrimPrefix("*").Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

        return [];
    }
}
#endif
