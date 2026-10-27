using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Dependinator.UI.Diagrams.Ai;

// The little markdown an AI's free text uses (Claude Code's fix report): paragraphs, "- " lists,
// **bold** and `code`, as HTML. Everything else is shown as text, encoded first, so the result is
// safe to render as markup.
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
}
