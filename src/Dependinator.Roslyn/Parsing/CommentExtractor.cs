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

            var lineSpan = Locations.GetSourceSpan(declarationNode.GetLocation());
            if (!IsMatchingFileSpan(lineSpan, fileSpan))
                continue;

            return GetLeadingComment(declarationNode);
        }

        return NoValue.String;
    }

    public static (IReadOnlyList<(string Comment, FileSpan Span)> Comments, FileSpan? FileSpan) GetNamespaceComments(
        INamespaceSymbol ns
    )
    {
        // A namespace can be declared across many files; its comments are the leading comments of
        // its declarations (a comment placed directly above `namespace X;`), in path order. A comment
        // with a blank line below it is none: it is likely a file header, such as a license.
        // A declaration of `namespace X.Y;` also declares the parent namespace X, so only
        // declarations whose written name matches the namespace exactly are considered
        // (a comment above `namespace X.Y;` must not become the description of X).
        // The span is the first commented declaration's location, or the first declaration's
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

        List<(string Comment, FileSpan Span)> comments = [];
        foreach (var (declaration, span) in declarations)
        {
            if (GetLeadingComment(declaration, isBlankLineBelowAllowed: false) is { } comment)
                comments.Add((comment, span));
        }

        FileSpan? fileSpan = comments.Count > 0 ? comments[0].Span : declarations.FirstOrDefault().Span;
        return (comments, fileSpan);
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
    // higher up in the file is not treated as the declaration's comment). A blank line between
    // the comment and the declaration is passed only when isBlankLineBelowAllowed.
    static string? GetLeadingComment(SyntaxNode declarationNode, bool isBlankLineBelowAllowed = true)
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
                endOfLineCount++;
                if (endOfLineCount > 1 && (isInsideCommentBlock || !isBlankLineBelowAllowed))
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

        // "\n" rather than the platform's line end, so a model is the same parsed on any platform
        var comment = string.Join("\n", commentLines);
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
