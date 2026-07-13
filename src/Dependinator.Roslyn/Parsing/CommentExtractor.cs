using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dependinator.Roslyn.Parsing;

static class CommentExtractor
{
    // Returns the comment directly above the symbol's declaration at fileSpan, or
    // NoValue.String when there is none (NoValue rather than null so that an existing
    // description in the model is cleared when the comment has been removed).
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

    public static (string? Comment, FileSpan? FileSpan) GetNamespaceCommentAndSpan(INamespaceSymbol ns)
    {
        // A namespace can be declared across many files; use the first declaration that
        // has a leading comment (e.g. a comment placed directly above `namespace X;`).
        // A declaration of `namespace X.Y;` also declares the parent namespace X, so only
        // declarations whose written name matches the namespace exactly are considered
        // (a comment above `namespace X.Y;` must not become the description of X).
        // The span is the commented declaration's location, or the first declaration's
        // location if none has a comment, so "show source" can navigate to a place where
        // a namespace comment can be edited or added.
        var namespaceName = ns.ToDisplayString();
        var declarations = ns
            .DeclaringSyntaxReferences.Select(syntaxRef => syntaxRef.GetSyntax())
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Where(declaration => declaration.Name.ToString() == namespaceName)
            .Select(declaration => (Declaration: declaration, Span: GetNamespaceNameSpan(declaration)))
            .OrderBy(d => d.Span.Path, StringComparer.Ordinal)
            .ThenBy(d => d.Span.StartLine)
            .ToList();

        foreach (var (declaration, span) in declarations)
        {
            var comment = GetLeadingComment(declaration);
            if (!string.IsNullOrWhiteSpace(comment))
                return (comment, span);
        }

        return (null, declarations.Count > 0 ? declarations[0].Span : null);
    }

    static FileSpan GetNamespaceNameSpan(BaseNamespaceDeclarationSyntax declaration)
    {
        // Use the line of the namespace name rather than the whole declaration node,
        // since a file-scoped namespace declaration spans the entire file.
        var lineSpan = declaration.Name.GetLocation().GetLineSpan();
        return new FileSpan(lineSpan.Path, lineSpan.StartLinePosition.Line, lineSpan.StartLinePosition.Line);
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

    // Collects the comment block adjacent to the declaration, walking the leading trivia
    // backwards from the declaration and stopping at a blank line (so a detached comment
    // higher up in the file is not treated as the declaration's comment).
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

    // Strips the comment syntax ("///", "//" or "/* ... */" with "*" line prefixes) and
    // returns the trimmed, non-empty comment lines
    static IEnumerable<string> NormalizeCommentLines(SyntaxTrivia commentTrivia)
    {
        var text = commentTrivia.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
            return [];

        string linePrefix;
        if (text.StartsWith("///"))
            linePrefix = "///";
        else if (text.StartsWith("//"))
            linePrefix = "//";
        else if (text.StartsWith("/*"))
        {
            text = text.TrimPrefix("/*").TrimSuffix("*/");
            linePrefix = "*";
        }
        else
            return [];

        return text.Split('\n')
            .Select(line => line.Replace("\r", "").Trim())
            .Select(line => line.TrimPrefix(linePrefix).Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }
}
