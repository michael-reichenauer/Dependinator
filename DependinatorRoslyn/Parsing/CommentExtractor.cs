using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DependinatorRoslyn.Parsing;

static class CommentExtractor
{
    public static string? GetLeadingCommentOrNoValue(ISymbol symbol, FileSpan fileSpan)
    {
        if (fileSpan == NoValue.FileSpan)
            return NoValue.String;

        foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            if (!TryGetDeclarationNode(syntaxRef.GetSyntax(), out var declarationNode))
                continue;

            var lineSpan = declarationNode.GetLocation().GetLineSpan();
            if (!IsMatchingFileSpan(lineSpan, fileSpan))
                continue;

            return GetLeadingComment(declarationNode);
        }

        return NoValue.String;
    }

    static bool TryGetDeclarationNode(SyntaxNode syntaxNode, out SyntaxNode declarationNode)
    {
        foreach (var node in syntaxNode.AncestorsAndSelf())
        {
            if (node is MemberDeclarationSyntax or EnumMemberDeclarationSyntax)
            {
                declarationNode = node;
                return true;
            }
        }

        declarationNode = null!;
        return false;
    }

    static bool IsMatchingFileSpan(FileLinePositionSpan lineSpan, FileSpan fileSpan)
    {
        if (lineSpan.Path != fileSpan.Path)
            return false;

        return fileSpan.EndLine >= lineSpan.StartLinePosition.Line
            && fileSpan.StartLine <= lineSpan.EndLinePosition.Line;
    }

    static string? GetLeadingComment(SyntaxNode declarationNode)
    {
        var leadingTrivia = declarationNode.GetLeadingTrivia();
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
