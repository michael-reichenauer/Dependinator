using System.Text.RegularExpressions;

namespace Dependinator.Core.Parsing.Utils;

// Splits a node's leading comment into the node description and line (dependency) descriptions.
// A comment line like "-> Some.Target: text" describes the diagram line from the commented
// node to the target node. The target is a full node name or a name relative to an ancestor
// of the source node. Lines before the first arrow line are the node description; non-arrow
// lines after an arrow line continue the most recent arrow's text (wrapped text).
// A description only takes effect if a line between the resolved source and target nodes
// actually exists in the model; otherwise it is silently unused.
static class CommentDescriptions
{
    static readonly Regex ArrowLineRegex = new(@"^->\s*(?<target>[^\s:]+)\s*:?\s*(?<text>.*)$", RegexOptions.Compiled);

    public record Result(string? NodeDescription, IReadOnlyList<(string Target, string Text)> LineDescriptions);

    public static Result Parse(string comment)
    {
        if (!comment.Contains("->"))
            return new Result(comment, []);

        List<string> descriptionLines = [];
        List<(string Target, string Text)> lineDescriptions = [];

        foreach (var line in comment.Split('\n'))
        {
            var trimmedLine = line.Trim();
            var match = ArrowLineRegex.Match(trimmedLine);
            if (match.Success)
            {
                lineDescriptions.Add((match.Groups["target"].Value, match.Groups["text"].Value.Trim()));
                continue;
            }

            if (lineDescriptions.Count > 0)
            { // Continuation of the most recent arrow line's text
                var (target, text) = lineDescriptions[^1];
                lineDescriptions[^1] = (target, text == "" ? trimmedLine : $"{text} {trimmedLine}");
                continue;
            }

            descriptionLines.Add(trimmedLine);
        }

        var nodeDescription = string.Join(Environment.NewLine, descriptionLines).Trim();
        return new Result(nodeDescription == "" ? null : nodeDescription, lineDescriptions);
    }
}
