using System.Text;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.Ai.PromptParts;
using NodeType = Dependinator.Core.Parsing.NodeType;

namespace Dependinator.UI.Diagrams.Editing;

// The text that asks an AI for the labels of several lines at once (LineLabelsDialog), so they
// share a style: the nodes at the lines' ends with their comments, each line by number with the
// links it stands for, the labels other lines have, and where the host can read the files, the
// source of the types (or modules, resources) that make most of the links.
static class LineLabelsPrompt
{
    // The most lines in one question: the answer stays readable, and the prompt within bounds.
    internal const int MaxLines = 40;
    const int MaxLinksPerLine = 4;
    const int MaxSourceUnits = 12;

    // The lines to label inside the scope (a container, or the root for the whole model): lines
    // between siblings that stand for links and have no label yet, drawn like the nodes (not
    // hidden or dimmed), no rows of a card, no pure configuration plumbing and nothing to or from
    // the external references (two opposite lines that say little). Outer levels come first,
    // then the lines with most links, so a cut at MaxLines keeps the lines that say most. Lines
    // asked about before (and left without a label by the AI or the user) come last, so asking
    // again reaches the rest. Total is the number of such lines before the cut.
    public static (IReadOnlyList<Line> Lines, int Total) Candidates(
        IModel model,
        Node scope,
        IReadOnlySet<LineId>? askedBefore = null
    )
    {
        List<Line> all =
        [
            .. model
                .Lines.Values.Where(l => IsCandidate(l) && IsInside(l.Source, scope))
                .Select(l => (Line: l, Depth: l.Source.Ancestors().Count()))
                .OrderBy(c => askedBefore?.Contains(c.Line.Id) ?? false)
                .ThenBy(c => c.Depth)
                .ThenByDescending(c => c.Line.Links.Count)
                .ThenBy(c => c.Line.Source.LongName, StringComparer.Ordinal)
                .ThenBy(c => c.Line.Target.LongName, StringComparer.Ordinal)
                .Select(c => c.Line),
        ];
        return ([.. all.Take(MaxLines)], all.Count);
    }

    static bool IsCandidate(Line line) =>
        line.Source.Parent is not null
        && line.Source.Parent == line.Target.Parent
        && !line.HasLabel
        && !line.IsEmpty
        && !line.IsDirect
        && !line.IsConfiguration
        && line.IsVisible
        && !IsLeftOut(line.Source)
        && !IsLeftOut(line.Target);

    static bool IsLeftOut(Node node) => node.Type.IsMember || node.Type == NodeType.Externals;

    static bool IsInside(Node node, Node scope) => scope.IsRoot || node.Ancestors().Contains(scope);

    // The code to show with the prompt: the types (or modules, resources) that make the lines'
    // links, those with most links first.
    public static IReadOnlyList<SourceRequest> GetSourceRequests(IReadOnlyList<Line> lines)
    {
        List<SourceUnit> units =
        [
            .. lines
                .SelectMany(l => l.Links)
                .Distinct()
                .GroupBy(l => Unit(l.Source))
                .Where(g => HasSource(g.Key))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key.LongName, StringComparer.Ordinal)
                .Take(MaxSourceUnits)
                .Select(SourceOf),
        ];
        return units.Count > 0 ? [new SourceRequest("The code behind the lines", units)] : [];
    }

    // The lines are numbered as given: a line that is gone (null) keeps its number, so the
    // answer's numbers still mean the same lines.
    public static string Build(IModel model, IReadOnlyList<Line?> lines, IReadOnlyList<SourceSection>? sources = null)
    {
        List<Line> present = [.. lines.OfType<Line>()];
        if (present.Count == 0)
            return "";

        var text = new StringBuilder();
        text.AppendLine(
            "I use Dependinator, a tool that maps the structure and dependencies of a codebase or a cloud "
                + "deployment as a diagram of nested nodes (projects, namespaces, types; modules, resources) and "
                + "the lines between them. A line can have a short label drawn on it, with a smaller description "
                + "under it, to tell a reader of the diagram what the dependency is about. I want labels for the "
                + "lines listed below, in one consistent style, so the diagram reads as an architecture picture."
        );
        // The prompt may be pasted into an AI that can change the code (Claude Code, say).
        text.AppendLine()
            .AppendLine(
                "This is a question only: do not change, create or commit any files, and do not run commands that "
                    + "do. If you can read the code, use it to check your answer."
            );
        if (Codebase(present[0].Source) is { } codebase)
            text.AppendLine().AppendLine(codebase);

        AppendNodes(text, present);
        AppendLines(text, lines);
        AppendOtherLabels(text, model, "Labels other lines already have");

        if (sources is { Count: > 0 })
            AppendSource(text, model, sources, "The code that makes most of the links.");

        text.AppendLine().Append(TaskSection);
        return text.ToString();
    }

    static readonly string TaskSection = $"""
        ## How to answer

        Give each line a label that tells, at a glance, what the source does with the target or what flows across the line: "reads secrets", "sends orders", "stores models", "diagnostics to", "parses with". Prefer what the links and the code show over what the names suggest. A label is one to four words in lower case, at most {LineLabelSuggestion.MaxLabelLength} characters, read as "source <label> target", and it does not repeat the names of the ends. Use the same wording for the same kind of dependency, and follow the style of the labels other lines already have. Leave a line out when nothing better than "uses" can be said about it.

        A description is one short phrase, at most {LineLabelSuggestion.MaxDescriptionLength} characters, with the detail the label leaves out (which part is used, or why), or empty when the label says enough.

        Answer with one line per label in this format, and nothing else:

        <number>. <label> | <description, or empty>

        """;

    // Each end once, with what it is and its comment.
    static void AppendNodes(StringBuilder text, IReadOnlyList<Line> lines)
    {
        text.AppendLine().AppendLine("## The nodes at the ends of the lines").AppendLine();
        var nodes = lines
            .SelectMany(l => new[] { l.Source, l.Target })
            .Distinct()
            .OrderBy(n => n.LongName, StringComparer.Ordinal);
        foreach (var node in nodes)
            text.AppendLine($"- {Kind(node.Type)} `{node.LongName}`{Described(node)}");
    }

    static void AppendLines(StringBuilder text, IReadOnlyList<Line?> lines)
    {
        text.AppendLine().AppendLine("## The lines").AppendLine();
        text.AppendLine("Each line with the links in the code it stands for (source → what it uses).");
        text.AppendLine();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i] is not { } line)
                continue;
            var inherits = line.IsInheritance ? " (inherits or implements)" : "";
            text.AppendLine($"{i + 1}. `{line.Source.LongName}` → `{line.Target.LongName}`{inherits}");
            AppendLineLinks(text, line);
        }
    }

    static void AppendLineLinks(StringBuilder text, Line line)
    {
        var container = line.Source.Parent!;
        var bySource = line
            .Links.GroupBy(l => Name(Member(l.Source), container))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
        foreach (var group in bySource.Take(MaxLinksPerLine))
            text.AppendLine($"   - {group.Key} → {Targets(group, container)}");
        if (bySource.Count > MaxLinksPerLine)
            text.AppendLine($"   - … and links from {bySource.Count - MaxLinksPerLine} more");
    }
}
