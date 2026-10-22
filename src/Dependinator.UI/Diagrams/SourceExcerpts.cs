using FileSpan = Dependinator.Core.Parsing.FileSpan;

namespace Dependinator.UI.Diagrams;

// A type (or module, resource) whose source to show: where it is, and the declaration lines of
// the members that matter (the model knows a member's first line only).
record SourceUnit(FileSpan Span, IReadOnlyList<int> MemberLines);

// Consecutive lines of a source file, from StartLine (1-based).
record SourceExcerpt(string Path, int StartLine, IReadOnlyList<string> Lines)
{
    public int EndLine => StartLine + Lines.Count - 1;
}

// Cuts the parts of a source file worth showing an AI: all of a short file (a comment above the
// type may say why it is where it is), else the type with the comments above it, else, for a
// long type, the members that matter, each with its comments.
static class SourceExcerpts
{
    const int MaxWholeFileLines = 120;
    const int MaxTypeLines = 150;
    const int MaxMemberLines = 40;

    public static IReadOnlyList<SourceExcerpt> Cut(SourceUnit unit, string fileText)
    {
        var lines = fileText.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        var path = unit.Span.Path;
        if (lines.Length <= MaxWholeFileLines)
            return [new(path, 1, lines)];

        var typeStart = CommentsStart(lines, Math.Min(unit.Span.StartLine, lines.Length));
        var typeEnd = Math.Min(unit.Span.EndLine, lines.Length);
        if (typeEnd - typeStart + 1 <= MaxTypeLines)
            return [Excerpt(path, lines, typeStart, typeEnd)];

        var ranges = unit
            .MemberLines.Where(line => line >= 1 && line <= lines.Length)
            .Order()
            .Select(line => (Start: CommentsStart(lines, line), End: MemberEnd(lines, line)));
        return [.. Merge(ranges).Select(r => Excerpt(path, lines, r.Start, r.End))];
    }

    static SourceExcerpt Excerpt(string path, string[] lines, int start, int end) =>
        new(path, start, lines[(start - 1)..end]);

    // The first line of the comments, attributes or decorators right above the line.
    static int CommentsStart(string[] lines, int line)
    {
        while (line > 1 && IsCommentOrAttribute(lines[line - 2]))
            line--;
        return line;
    }

    static bool IsCommentOrAttribute(string line)
    {
        var text = line.TrimStart();
        return text.StartsWith("//")
            || text.StartsWith("/*")
            || text.StartsWith('*')
            || text.StartsWith('[')
            || text.StartsWith('@');
    }

    // Where the member declared at the line ends: when its braces close, or, without braces,
    // when its statement ends or a blank line follows. A guess, good enough to show it; capped.
    static int MemberEnd(string[] lines, int line)
    {
        var last = Math.Min(lines.Length, line + MaxMemberLines - 1);
        var depth = 0;
        var hasBraces = false;
        for (var current = line; current < last; current++)
        {
            var text = lines[current - 1];
            foreach (var c in text)
            {
                if (c == '{')
                {
                    depth++;
                    hasBraces = true;
                }
                else if (c == '}')
                    depth--;
            }
            var hasEnded = hasBraces
                ? depth <= 0
                : text.TrimEnd().EndsWith(';') || string.IsNullOrWhiteSpace(lines[current]);
            if (hasEnded)
                return current;
        }
        return last;
    }

    // Overlapping or adjacent ranges as one, so two members next to each other show once.
    static IEnumerable<(int Start, int End)> Merge(IEnumerable<(int Start, int End)> sortedRanges)
    {
        (int Start, int End)? pending = null;
        foreach (var range in sortedRanges)
        {
            if (pending is { } open && range.Start <= open.End + 1)
            {
                pending = (open.Start, Math.Max(open.End, range.End));
                continue;
            }
            if (pending is { } done)
                yield return done;
            pending = range;
        }
        if (pending is { } last)
            yield return last;
    }
}
