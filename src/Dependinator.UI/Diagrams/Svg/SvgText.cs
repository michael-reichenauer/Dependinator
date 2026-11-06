using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using Dependinator.UI.Modeling.Models;

// Text in the diagram: the font sizes and how they follow the zoom, rough text widths, clipping,
// and the wrapped node descriptions.
namespace Dependinator.UI.Diagrams.Svg;

static partial class SvgText
{
    internal const int FontSize = 8; // Also the member icon size, see NodeAnchors
    internal const int DescriptionFontSize = 6;

    // Name/description text stops scaling with zoom beyond this factor, so zooming in reveals
    // more description text instead of just growing the letters. A default-size node opens as a
    // container at NodeViewPolicy.MinContainerZoom (2.0), so text still grows a bit "into" the
    // node before plateauing at 24px names and 18px descriptions. Text follows the zoom, not the
    // node's size, so labels of small and large nodes stay equally readable.
    internal const double MaxTextZoom = 3.0;
    internal const int DescriptionMinWidth = 25;
    internal const int DescriptionMaxWidth = 100;
    internal const int DescriptionMaxLines = 7;
    internal const int IconDescriptionMaxLines = 5;
    internal const double DescriptionCharWidthFactor = 0.45;
    internal const double DescriptionLineHeightFactor = 1.2;
    internal const double DescriptionLineGap = 1;

    // Offset (in em) from the text's y down to the first line's alphabetic baseline. This lets us
    // top-align the description using the universally-supported alphabetic baseline instead of
    // `dominant-baseline: hanging`, which WebKit (iPad Safari) does not apply to <tspan> children.
    internal const double DescriptionFirstLineOffsetFactor = 0.8;

    // Fraction of the average glyph advance relative to the font size.
    internal const double AverageCharWidthFactor = 0.6;

    // Effective zoom for text and header icons: follows the diagram zoom until MaxTextZoom,
    // then stays capped so text keeps a readable size instead of growing with the node.
    internal static double TextZoom(double parentZoom) => Math.Min(parentZoom, MaxTextZoom);

    // Once the font size is capped, further zooming leaves unused space below the node (layout
    // distances keep scaling while text does not). Convert that surplus into extra description
    // lines, keeping the same pixel footprint as the uncapped layout would have used.
    internal static int ScaledMaxLines(int maxLines, double parentZoom, double textZoom) =>
        textZoom <= 0 ? maxLines : (int)(maxLines * parentZoom / textZoom);

    // Rough width of rendered text in the diagram font (average glyph advance), used e.g. to
    // place the manual marker after a name.
    internal static double EstimateTextWidth(string text, double fontSize) =>
        string.IsNullOrEmpty(text) ? 0 : text.Length * fontSize * AverageCharWidthFactor;

    // Clip, but nothing rather than a stub of a word or two letters.
    internal static string ClipRow(string text, int maxChars) =>
        text.Length <= maxChars ? text
        : maxChars < 6 ? ""
        : Clip(text, maxChars);

    internal static int MaxChars(double width, double fontSize) =>
        fontSize <= 0 ? 0 : Math.Max(1, (int)(width / (fontSize * AverageCharWidthFactor)));

    internal static string Clip(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[..Math.Max(1, maxChars - 1)].TrimEnd() + "…";

    internal static string Encode(string text) => HttpUtility.HtmlEncode(text);

    internal static string BuildDescriptionSvg(
        Node node,
        double x,
        double y,
        double fontSize,
        string cssClass,
        string textOpacity,
        int maxWidth,
        int maxLines
    )
    {
        if (maxLines <= 0)
            return "";
        var lines = GetDescriptionLines(node.Description, maxWidth, maxLines);
        if (lines.Count == 0)
            return "";

        var lineHeight = fontSize * DescriptionLineHeightFactor;
        var firstLineOffset = fontSize * DescriptionFirstLineOffsetFactor;
        var tspans = string.Join(
            "\n",
            lines.Select(
                (line, i) =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"""<tspan x="{x:0.##}" dy="{(i == 0 ? firstLineOffset : lineHeight):0.##}">{HttpUtility.HtmlEncode(line)}</tspan>"""
                    )
            )
        );

        return string.Create(
            CultureInfo.InvariantCulture,
            $"""<text x="{x:0.##}" y="{y:0.##}" class="{cssClass}" font-size="{fontSize:0.##}px" {textOpacity}>{tspans}</text>"""
        );
    }

    // Estimate how many characters fit across a node of the given pixel width, so wider
    // (zoomed-in) containers wrap at a wider line than small icon nodes. Clamped to a
    // readable floor and a sane cap.
    internal static int DescriptionWidthForNode(double availableWidth, double fontSize)
    {
        var charWidth = fontSize * DescriptionCharWidthFactor;
        if (charWidth <= 0)
            return DescriptionMinWidth;

        var chars = (int)(availableWidth / charWidth);
        return Math.Clamp(chars, DescriptionMinWidth, DescriptionMaxWidth);
    }

    internal static IReadOnlyList<string> GetDescriptionLines(string? description, int maxWidth, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(description))
            return [];

        // Collapse to a single line and strip XML-doc tags like <summary>.
        var text = XmlTagRegex().Replace(description, " ");
        text = WhitespaceRegex().Replace(text, " ").Trim();
        if (text.Length == 0)
            return [];

        var lines = WrapText(text, maxWidth);
        if (lines.Count <= maxLines)
            return lines;

        // Truncate to maxLines and mark the last kept line with an ellipsis.
        lines = lines.Take(maxLines).ToList();
        var last = lines[^1];
        if (last.Length + 1 > maxWidth)
            last = last[..(maxWidth - 1)].TrimEnd();
        lines[^1] = last + "…";
        return lines;
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex XmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    static List<string> WrapText(string text, int maxWidth)
    {
        var lines = new List<string>();
        var current = "";

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var remaining = word;

            // Hard-break words that are longer than a full line.
            while (remaining.Length > maxWidth)
            {
                if (current.Length > 0)
                {
                    lines.Add(current);
                    current = "";
                }
                lines.Add(remaining[..maxWidth]);
                remaining = remaining[maxWidth..];
            }

            if (remaining.Length == 0)
                continue;

            if (current.Length == 0)
                current = remaining;
            else if (current.Length + 1 + remaining.Length <= maxWidth)
                current += " " + remaining;
            else
            {
                lines.Add(current);
                current = remaining;
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        return lines;
    }
}
