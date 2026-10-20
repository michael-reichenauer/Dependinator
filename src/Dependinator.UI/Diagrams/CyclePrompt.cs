using System.Text;
using System.Text.RegularExpressions;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using NodeType = Dependinator.Core.Parsing.NodeType;

namespace Dependinator.UI.Diagrams;

// Code a prompt would show: for one direction of the cycle, the types that make its links.
record SourceRequest(string Direction, IReadOnlyList<SourceUnit> Units);

// The code read for a SourceRequest.
record SourceSection(string Direction, IReadOnlyList<SourceExcerpt> Excerpts);

// The text that asks an AI whether a circular dependency is intended, and can be accepted with
// a reason to store, or should be broken, and how. The user pastes it into an AI chat. Its shape
// was tried on real cycles: a sound verdict needs the members with their descriptions and the
// concrete links in each direction, and an answer that relies on code it cannot see must say so,
// hence the Assumptions line. Links are listed per member that makes them (a property's getter
// counts as the property; target members are grouped under their type) while that stays short,
// and per type otherwise, so a cycle with thousands of links still gives a prompt of a few pages.
// Where the host can read the files, the source behind the thin directions (made by one or two
// types) is shown too: without it, an answer can propose a move that would not compile. Thin is
// counted in types, not links: a class that classifies an enum makes a link per enum value.
static partial class CyclePrompt
{
    const int MaxDetailedSources = 12;
    const int MaxLinesPerDirection = 30;
    const int MaxTargetMembers = 8;
    const int MaxDescriptionLength = 300;
    const int MaxReasonLength = 200;
    const int MaxSourceUnitsPerDirection = 2;
    const int MaxSourceLines = 400;

    // The code to show with the prompt: for each direction made by only a type or two (a likely
    // one to break, or to explain), those types; the thinnest directions first.
    public static IReadOnlyList<SourceRequest> GetSourceRequests(IModel model, Cycle cycle)
    {
        if (!model.Nodes.TryGetValue(cycle.ContainerId, out var container))
            return [];
        var thin = LinksBetween(Members(model, cycle))
            .Select(d =>
                (Direction: d.Key, ByType: d.Value.GroupBy(l => Unit(l.Source)).ToList(), Links: d.Value.Count)
            )
            .Where(d => d.ByType.Count <= MaxSourceUnitsPerDirection)
            .OrderBy(d => d.ByType.Count)
            .ThenBy(d => d.Links);

        List<SourceRequest> requests = [];
        foreach (var (direction, byType, _) in thin)
        {
            List<SourceUnit> units = [.. byType.Where(g => HasSource(g.Key)).Select(SourceOf)];
            if (units.Count > 0)
                requests.Add(new($"{Name(direction.From, container)} → {Name(direction.To, container)}", units));
        }
        return requests;
    }

    // A type's span, and the declaration lines of its members that make the links.
    static SourceUnit SourceOf(IGrouping<Node, Link> linksOfType)
    {
        var span = linksOfType.Key.FileSpan!;
        var memberLines = linksOfType
            .Select(l => Member(l.Source))
            .Where(m => m != linksOfType.Key && m.FileSpan?.Path == span.Path)
            .Select(m => m.FileSpan!.StartLine)
            .Distinct();
        return new SourceUnit(span, [.. memberLines]);
    }

    public static string Build(IModel model, Cycle cycle, IReadOnlyList<SourceSection>? sources = null)
    {
        if (!model.Nodes.TryGetValue(cycle.ContainerId, out var container))
            return "";
        var members = Members(model, cycle);

        var text = new StringBuilder();
        text.AppendLine(
            "I use Dependinator, a tool that maps the structure and dependencies of a codebase. It found a "
                + "circular dependency, and I need to decide whether to accept it as intended (with a short "
                + "reason that is stored with the model) or to break it."
        );
        if (Codebase(container) is { } codebase)
            text.AppendLine().AppendLine(codebase);
        AppendCycle(text, cycle, container, members);

        if (sources is { Count: > 0 })
            AppendSource(text, model, sources);

        text.AppendLine().Append(TaskSection);
        return text.ToString();
    }

    // The prompt for an AI that can change the code (Claude Code, say), to break the cycle as
    // Claude's verdict suggested: the cycle, the suggested change with the assumptions to check
    // first, and where to look. It shows no source, since such an AI reads the code itself.
    public static string BuildFix(IModel model, Cycle cycle, CycleSuggestion suggestion)
    {
        if (!model.Nodes.TryGetValue(cycle.ContainerId, out var container))
            return "";
        var members = Members(model, cycle);

        var text = new StringBuilder();
        text.AppendLine(
            "Please break a circular dependency in this codebase. Dependinator, a tool that maps the structure "
                + "and dependencies of the code, found it, and a review of it suggested how to break it."
        );
        AppendCycle(text, cycle, container, members);

        text.AppendLine().AppendLine("## The suggested change").AppendLine();
        text.AppendLine(suggestion.Change);
        if (suggestion.Why != "")
            text.AppendLine().AppendLine($"Why: {suggestion.Why}");
        if (suggestion.Assumptions is not ("" or "None" or "None."))
            text.AppendLine().AppendLine($"Assumptions to check first: {suggestion.Assumptions}");

        var files = GetSourceRequests(model, cycle)
            .SelectMany(r => r.Units)
            .Select(u => DisplayPath(model, u.Span.Path))
            .Distinct()
            .ToList();
        if (files.Count > 0)
        {
            text.AppendLine().AppendLine("## Where to look").AppendLine();
            text.AppendLine("The code behind the directions made by only one or two types:");
            foreach (var file in files)
                text.AppendLine($"- {file}");
        }

        text.AppendLine().Append(FixTaskSection);
        return text.ToString();
    }

    const string FixTaskSection = """
        ## Your task

        1. Check the assumptions above in the code first. If one does not hold, say so, and propose another change before editing.
        2. Make the smallest change that removes one direction of the cycle, without changing behavior.
        3. Update the code that refers to what you moved or renamed.
        4. Build, and run the tests.
        5. Summarize what you changed. Dependinator shows whether the cycle is gone after its next parse.

        """;

    // The cycle's members, and the links between them per direction.
    static void AppendCycle(StringBuilder text, Cycle cycle, Node container, List<Node> members)
    {
        text.AppendLine().AppendLine("## The cycle").AppendLine();
        text.AppendLine(
            container.IsRoot
                ? "At the top level of the model, these siblings depend on each other in a circle:"
                : $"In {Kind(container.Type)} {container.LongName}, these siblings depend on each other in a circle:"
        );
        text.AppendLine();
        foreach (var member in members)
            text.AppendLine($"- {Name(member, container)} ({Kind(member.Type)}){Described(member)}");
        if (cycle.AcceptedBefore is { } before)
            text.AppendLine().AppendLine(AcceptedBefore(cycle, before));

        text.AppendLine().AppendLine("## The dependencies that form it");
        var links = LinksBetween(members);
        foreach (var from in members)
        {
            foreach (var to in members)
            {
                if (!links.TryGetValue((from, to), out var directionLinks))
                    continue;
                var count = directionLinks.Count;
                text.AppendLine();
                text.AppendLine(
                    $"{Name(from, container)} → {Name(to, container)} ({count} {(count == 1 ? "link" : "links")})"
                );
                AppendLinks(text, directionLinks, container);
            }
        }
    }

    static List<Node> Members(IModel model, Cycle cycle) =>
        [.. cycle.Members.Select(m => model.Nodes.GetValueOrDefault(m.Id)).OfType<Node>()];

    // Types, and the modules and resources of infrastructure models, have spans worth showing.
    static bool HasSource(Node node) =>
        node.FileSpan is not null && (node.Type.IsType || node.Type is NodeType.Module or NodeType.Resource);

    static void AppendSource(StringBuilder text, IModel model, IReadOnlyList<SourceSection> sources)
    {
        text.AppendLine().AppendLine("## Source").AppendLine();
        text.AppendLine(
            "The code behind the directions made by only one or two types, the ones most likely to be broken or to be explained."
        );
        var shownLines = 0;
        foreach (var source in sources)
        {
            text.AppendLine().AppendLine($"### {source.Direction}");
            foreach (var excerpt in source.Excerpts)
            {
                if (shownLines + excerpt.Lines.Count > MaxSourceLines)
                {
                    text.AppendLine().AppendLine("(The rest of the source is left out.)");
                    return;
                }
                shownLines += excerpt.Lines.Count;
                text.AppendLine();
                text.AppendLine($"{DisplayPath(model, excerpt.Path)}, lines {excerpt.StartLine}–{excerpt.EndLine}:");
                text.AppendLine().AppendLine($"```{FenceLanguage(excerpt.Path)}");
                foreach (var line in excerpt.Lines)
                    text.AppendLine(line);
                text.AppendLine("```");
            }
        }
    }

    // A path relative to the model's folder (where the .sln is), when inside it.
    static string DisplayPath(IModel model, string path)
    {
        var folder = Path.GetDirectoryName(model.Path);
        if (string.IsNullOrEmpty(folder))
            return path;
        var relative = Path.GetRelativePath(folder, path);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? path : relative;
    }

    static string FenceLanguage(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".bicep" => "bicep",
            _ => "",
        };

    static readonly string TaskSection = $"""
        ## Your task

        Decide whether this cycle should be accepted as intended or broken. Base the decision on the dependencies above: how many links go each way, what they are for, and whether the members are really one concept.

        Answer in this format:

        Verdict: Accept or Break
        Why: 2–4 sentences.
        Assumptions: what you assumed about code that is not shown here, or "None".
        Reason to store: (only if Accept) at most {MaxReasonLength} characters, specific to these members, for a future maintainer; not a general statement about cycles.
        Change: (only if Break) the smallest concrete change that removes one direction, and which direction.

        If the information is not enough to decide, say what you would need to see instead of guessing.

        """;

    // The codebase the cycle is in: the model's top-level node above it (the solution, say).
    static string? Codebase(Node container)
    {
        var top = container.AncestorsAndSelf().FirstOrDefault(n => n.Parent is { IsRoot: true });
        if (top is null)
            return null;
        return top.Description is { } description
            ? $"The codebase is {top.DisplayName}: {Clip(description)}"
            : $"The codebase is {top.DisplayName}.";
    }

    static string AcceptedBefore(Cycle cycle, AcceptedCycle before)
    {
        var joined = string.Join(
            ", ",
            cycle.Members.Where(m => !before.MemberNames.Contains(m.NodeName)).Select(m => m.Name)
        );
        if (before.IsToFix)
        {
            var change = before.Reason == "" ? "" : $", with the planned change \"{before.Reason}\"";
            return $"Part of it was marked to fix before{change}, but {joined} joined the cycle since.";
        }
        var reason = before.Reason == "" ? "without a reason" : $"with the reason \"{before.Reason}\"";
        return $"Part of it was accepted before, {reason}, but {joined} joined the cycle since.";
    }

    // Per direction between two members, the links from inside the one to inside the other.
    static Dictionary<(Node From, Node To), List<Link>> LinksBetween(IReadOnlyList<Node> members)
    {
        var memberSet = members.ToHashSet();
        var links = new Dictionary<(Node, Node), List<Link>>();
        foreach (var from in members)
        {
            foreach (var link in from.DescendantsAndSelfPreOrder().SelectMany(n => n.SourceLinks))
            {
                var to = link.Target.AncestorsAndSelf().FirstOrDefault(memberSet.Contains);
                if (to is null || to == from)
                    continue;
                if (!links.TryGetValue((from, to), out var list))
                    links[(from, to)] = list = [];
                list.Add(link);
            }
        }
        return links;
    }

    static void AppendLinks(StringBuilder text, IReadOnlyList<Link> links, Node container)
    {
        // By name rather than node: nodes can share a display name (overloads, say), and a line
        // per node would repeat it.
        var bySource = links
            .GroupBy(l => Name(Member(l.Source), container))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
        if (bySource.Count <= MaxDetailedSources)
        {
            foreach (var group in bySource)
            {
                text.AppendLine($"- {group.Key} → {Targets(group, container)}");
                if (
                    group.Select(l => Member(l.Source).Description).FirstOrDefault(d => d is not null) is
                    { } description
                )
                    text.AppendLine($"  \"{Clip(description)}\"");
            }
            return;
        }

        var byType = links
            .GroupBy(l => Name(Unit(l.Source), container))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
        foreach (var group in byType.Take(MaxLinesPerDirection))
        {
            var targets = group.Select(l => Name(Unit(l.Target), container)).Distinct().Order(StringComparer.Ordinal);
            text.AppendLine($"- {group.Key} → {string.Join(", ", targets)}");
        }
        if (byType.Count > MaxLinesPerDirection)
            text.AppendLine($"- … and {byType.Count - MaxLinesPerDirection} more");
    }

    // The targets of one source's links, grouped by type, with the members used in parentheses:
    // "Parsing.NodeType (FieldMember, MethodMember)".
    static string Targets(IEnumerable<Link> links, Node container)
    {
        var byType = links
            .Select(l => l.Target)
            .GroupBy(t => Name(Unit(t), container))
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        return string.Join(
            ", ",
            byType.Select(group =>
            {
                List<string> used =
                [
                    .. group
                        .Where(t => t.Type.IsMember)
                        .Select(t => MemberLabel(Member(t)))
                        .Distinct()
                        .Order(StringComparer.OrdinalIgnoreCase),
                ];
                if (used.Count == 0)
                    return group.Key;
                IReadOnlyList<string> shown =
                    used.Count <= MaxTargetMembers ? used : [.. used.Take(MaxTargetMembers), "…"];
                return $"{group.Key} ({string.Join(", ", shown)})";
            })
        );
    }

    // A used member by its name alone: overloads read as one, and their parameters are noise here.
    // The C# parser types constructors as methods named after their type.
    static string MemberLabel(Node member)
    {
        var unit = Unit(member);
        var name = Name(member, unit).Split('(')[0];
        return member.Type == NodeType.ConstructorMember || name == unit.ShortName.Split('<')[0] ? "constructor" : name;
    }

    // The closest node that is not a member: a member's type, or the node itself. Made-up parents
    // in between are passed: a conversion operator's name has dots ("implicit operator A.B"), so
    // it is filed under parent nodes created for "A" inside its type. In a model without types
    // (tests), the closest node that is not a member.
    static Node Unit(Node node)
    {
        var nonMembers = node.AncestorsAndSelf().Where(n => !n.Type.IsMember).ToList();
        return nonMembers.FirstOrDefault(n => n.Type is not (NodeType.None or NodeType.Root or NodeType.Parent))
            ?? nonMembers[0];
    }

    // The member the node is part of (a getter or a local function counts as its property or
    // method), or the node itself when it is not a member.
    static Node Member(Node node)
    {
        if (!node.Type.IsMember)
            return node;
        while (node.Parent is { Type.IsMember: true } parent)
            node = parent;
        return node;
    }

    // The node's name relative to the given ancestor, its full name otherwise, without the
    // namespaces in generic arguments: "Result<IParser>", not "Result<global::Demo.IParser>".
    static string Name(Node node, Node relativeTo)
    {
        var prefix = relativeTo.LongName + ".";
        var name =
            !relativeTo.IsRoot && node.LongName.StartsWith(prefix, StringComparison.Ordinal)
                ? node.LongName[prefix.Length..]
                : node.LongName;
        return GlobalQualifier().Replace(name, "");
    }

    [GeneratedRegex(@"global::(\w+\.)*")]
    private static partial Regex GlobalQualifier();

    static string Described(Node node) => node.Description is { } description ? $": \"{Clip(description)}\"" : "";

    // A description on one line; a long one is cut after its last sentence that fits, or at a word.
    static string Clip(string description)
    {
        var text = string.Join(" ", description.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length <= MaxDescriptionLength)
            return text;
        var sentenceEnd = text.LastIndexOf(". ", MaxDescriptionLength, StringComparison.Ordinal);
        if (sentenceEnd > MaxDescriptionLength / 2)
            return text[..(sentenceEnd + 1)];
        var wordEnd = text.LastIndexOf(' ', MaxDescriptionLength);
        return text[..(wordEnd > 0 ? wordEnd : MaxDescriptionLength)] + "…";
    }

    static string Kind(NodeType type) =>
        type switch
        {
            NodeType.Solution => "solution",
            NodeType.SolutionFolder => "solution folder",
            NodeType.Assembly or NodeType.Dll or NodeType.Exe => "project",
            NodeType.Externals => "external references",
            NodeType.Namespace => "namespace",
            NodeType.ClassType => "class",
            NodeType.InterfaceType => "interface",
            NodeType.EnumType => "enum",
            NodeType.StructType => "struct",
            NodeType.RecordType => "record",
            NodeType.Type => "type",
            NodeType.Module => "module",
            NodeType.Resource => "resource",
            _ => "group",
        };
}
