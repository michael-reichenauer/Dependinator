using System.Text;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.Ai.PromptParts;

namespace Dependinator.UI.Diagrams.Editing;

// The text that asks an AI for a line's label and description (LineLabelDialog): the two ends with
// their comments, the links the line stands for (per member that makes them), the label the line
// has now, the labels on other lines (so a suggestion matches their style), and where the host can
// read the files, the source of the types that make the links.
static class LineLabelPrompt
{
    const int MaxSourceUnits = 4;

    // The code to show with the prompt: the types (or modules, resources) that make the links,
    // those with most links first.
    public static IReadOnlyList<SourceRequest> GetSourceRequests(Line line)
    {
        List<SourceUnit> units =
        [
            .. line
                .Links.GroupBy(l => Unit(l.Source))
                .Where(g => HasSource(g.Key))
                .OrderByDescending(g => g.Count())
                .Take(MaxSourceUnits)
                .Select(SourceOf),
        ];
        return units.Count > 0 ? [new SourceRequest($"{line.Source.LongName} → {line.Target.LongName}", units)] : [];
    }

    public static string Build(IModel model, Line line, IReadOnlyList<SourceSection>? sources = null)
    {
        var text = new StringBuilder();
        text.AppendLine(
            "I use Dependinator, a tool that maps the structure and dependencies of a codebase as a diagram of "
                + "nested nodes (projects, namespaces, types) and the lines between them. A line can have a short "
                + "label drawn on it, with a smaller description under it, to tell a reader of the diagram what "
                + "the dependency is about. I want a label for one line."
        );
        // The prompt may be pasted into an AI that can change the code (Claude Code, say).
        text.AppendLine()
            .AppendLine(
                "This is a question only: do not change, create or commit any files, and do not run commands that "
                    + "do. If you can read the code, use it to check your answer."
            );
        if (Codebase(line.Source) is { } codebase)
            text.AppendLine().AppendLine(codebase);

        AppendLine(text, model, line);
        AppendCurrentLabel(text, line);
        AppendOtherLabels(text, model, "Labels on other lines", line);

        if (sources is { Count: > 0 })
            AppendSource(text, model, sources, "The code of the types that make the links.");

        text.AppendLine().Append(TaskSection);
        return text.ToString();
    }

    static readonly string TaskSection = $"""
        ## How to answer

        Suggest a label that tells, at a glance, what the source does with the target or what flows across the line: "parses with", "reads settings", "sends orders", "stores models". Prefer what the links and the code show over what the names suggest. The label is one to four words in lower case, at most {LineLabelSuggestion.MaxLabelLength} characters, read as "source <label> target", and it does not repeat the names of the ends. If the other lines' labels have a style, follow it.

        The description is one short phrase, at most {LineLabelSuggestion.MaxDescriptionLength} characters, with the detail the label leaves out (which part is used, or why), or empty when the label says enough. Also give up to three other labels to choose from.

        Answer in this format, and nothing else:

        Label: <the label>
        Description: <the description, or empty>
        Alternatives: <other label>; <other label>; <other label>

        """;

    static void AppendLine(StringBuilder text, IModel model, Line line)
    {
        var (source, target) = (line.Source, line.Target);
        text.AppendLine().AppendLine("## The line").AppendLine();
        text.AppendLine($"From {Kind(source.Type)} {source.LongName}{Described(source)}");
        text.AppendLine($"to {Kind(target.Type)} {target.LongName}{Described(target)}");
        if (line.IsInheritance)
            text.AppendLine("The source inherits from, or implements, the target.");

        var links = line.Links.ToList();
        if (links.Count == 0)
        {
            text.AppendLine().AppendLine("The line was drawn by hand: no links in the code stand behind it.");
            return;
        }
        text.AppendLine()
            .AppendLine($"It stands for {links.Count} {(links.Count == 1 ? "link" : "links")} in the code:");
        AppendLinks(text, links, Container(model, source, target));
    }

    static void AppendCurrentLabel(StringBuilder text, Line line)
    {
        if (!line.HasLabel)
            return;
        text.AppendLine().AppendLine("## Its label now").AppendLine();
        text.AppendLine($"Label: {line.Label ?? ""}");
        text.AppendLine($"Description: {line.LabelDescription ?? ""}");
        text.AppendLine("Keep what is right in it, and improve the rest.");
    }

    // The closest node around both ends, so the links are named relative to it.
    static Node Container(IModel model, Node source, Node target)
    {
        var targetAncestors = target.Ancestors().ToHashSet();
        return source.Ancestors().FirstOrDefault(targetAncestors.Contains) ?? model.Root;
    }
}
