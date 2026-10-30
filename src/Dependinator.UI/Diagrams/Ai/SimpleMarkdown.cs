using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Dependinator.UI.Diagrams.Ai;

// The little markdown an AI's free text uses (Claude Code's fix report): paragraphs, "- " lists,
// **bold** and `code`, as HTML. Everything else is shown as text, encoded first, so the result is
// safe to render as markup. Parse gives the same as blocks of spans instead, for a component that
// renders some spans itself (the Ask panel turns a `node name` into a link to the node).
public static partial class SimpleMarkdown
{
    public static string ToHtml(string text)
    {
        var html = new StringBuilder();
        List<string> paragraph = [];
        List<string> items = [];

        void Flush()
        {
            if (paragraph.Count > 0)
                html.Append("<p>").Append(string.Join("<br>", paragraph)).Append("</p>");
            if (items.Count > 0)
                html.Append("<ul>").Append(string.Concat(items.Select(i => $"<li>{i}</li>"))).Append("</ul>");
            paragraph.Clear();
            items.Clear();
        }

        foreach (var rawLine in (text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line == "")
            {
                Flush();
                continue;
            }
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                if (paragraph.Count > 0)
                    Flush();
                items.Add(Inline(line[2..].Trim()));
                continue;
            }
            if (items.Count > 0)
                Flush();
            paragraph.Add(Inline(line));
        }
        Flush();
        return html.ToString();
    }

    static string Inline(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        encoded = CodeRegex().Replace(encoded, "<code>$1</code>");
        return BoldRegex().Replace(encoded, "<b>$1</b>");
    }

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    // The text as paragraphs and lists, each a list of lines (a list's items) made of spans. Also
    // reads "1. " items as a numbered list (blank lines between its items, as AIs often write, do
    // not end it; it starts at its first item's number) and "# " headings as a bold line.
    public static IReadOnlyList<MarkdownBlock> Parse(string text)
    {
        List<MarkdownBlock> blocks = [];
        List<IReadOnlyList<MarkdownSpan>> lines = [];
        var kind = MarkdownBlockKind.Paragraph;
        var start = 1;

        void Flush()
        {
            if (lines.Count > 0)
                blocks.Add(new MarkdownBlock(kind, [.. lines], start));
            lines.Clear();
        }

        void Add(MarkdownBlockKind lineKind, IReadOnlyList<MarkdownSpan> spans, int number = 1)
        {
            if (lineKind != kind)
                Flush();
            if (lines.Count == 0)
                start = number;
            kind = lineKind;
            lines.Add(spans);
        }

        foreach (var rawLine in (text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line == "")
            {
                if (kind != MarkdownBlockKind.NumberedList)
                    Flush();
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
                Add(MarkdownBlockKind.List, Spans(line[2..].Trim()));
            else if (NumberedItemRegex().Match(line) is { Success: true } item)
                Add(MarkdownBlockKind.NumberedList, Spans(line[item.Length..].Trim()), int.Parse(item.Groups[1].Value));
            else if (HeadingRegex().Match(line) is { Success: true } heading)
                Add(
                    MarkdownBlockKind.Paragraph,
                    [.. Spans(line[heading.Length..].Trim()).Select(s => s with { IsBold = true })]
                );
            else
                Add(MarkdownBlockKind.Paragraph, Spans(line));
        }
        Flush();
        return blocks;
    }

    // A line's text, bold and code spans, in order: code first, so "**" inside code stays code;
    // a "**" outside code turns bold on (when another follows) or off, so bold may hold code.
    static IReadOnlyList<MarkdownSpan> Spans(string line)
    {
        List<MarkdownSpan> spans = [];
        var isBold = false;
        var at = 0;
        var tokens = SpanTokenRegex().Matches(line).ToList();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var isCode = token.Value.StartsWith('`');
            if (!isCode && !isBold && !tokens.Skip(i + 1).Any(t => t.Value == "**"))
                continue; // An unmatched "**" is text.
            if (token.Index > at)
                spans.Add(new MarkdownSpan(line[at..token.Index], isBold, IsCode: false));
            if (isCode)
                spans.Add(new MarkdownSpan(token.Groups[1].Value, isBold, IsCode: true));
            else
                isBold = !isBold;
            at = token.Index + token.Length;
        }
        if (at < line.Length)
            spans.Add(new MarkdownSpan(line[at..], isBold, IsCode: false));
        return spans;
    }

    [GeneratedRegex(@"`([^`]+)`|\*\*")]
    private static partial Regex SpanTokenRegex();

    [GeneratedRegex(@"^(\d{1,3})[.)]\s+")]
    private static partial Regex NumberedItemRegex();

    [GeneratedRegex(@"^#{1,6}\s+")]
    private static partial Regex HeadingRegex();
}

public enum MarkdownBlockKind
{
    Paragraph,
    List,
    NumberedList,
}

// A paragraph's lines, or a list's items; Start is a numbered list's first number.
public record MarkdownBlock(MarkdownBlockKind Kind, IReadOnlyList<IReadOnlyList<MarkdownSpan>> Lines, int Start = 1);

// Plain text, not encoded: Razor encodes it when it renders.
public record MarkdownSpan(string Text, bool IsBold, bool IsCode);
