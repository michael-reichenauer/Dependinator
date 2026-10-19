using System.Text;
using System.Text.RegularExpressions;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using NodeType = Dependinator.Core.Parsing.NodeType;

namespace Dependinator.UI.Diagrams;

// The text that asks an AI whether a circular dependency is intended, and can be accepted with
// a reason to store, or should be broken, and how. The user pastes it into an AI chat. Its shape
// was tried on real cycles: a sound verdict needs the members with their descriptions and the
// concrete links in each direction, and an answer that relies on code it cannot see must say so,
// hence the Assumptions line. Links are listed per member that makes them (a property's getter
// counts as the property; target members are grouped under their type) while that stays short,
// and per type otherwise, so a cycle with thousands of links still gives a prompt of a few pages.
static partial class CyclePrompt
{
    const int MaxDetailedSources = 12;
    const int MaxLinesPerDirection = 30;
    const int MaxTargetMembers = 8;
    const int MaxDescriptionLength = 300;
    const int MaxReasonLength = 200;

    public static string Build(IModel model, Cycle cycle)
    {
        if (!model.Nodes.TryGetValue(cycle.ContainerId, out var container))
            return "";
        List<Node> members = [.. cycle.Members.Select(m => model.Nodes.GetValueOrDefault(m.Id)).OfType<Node>()];

        var text = new StringBuilder();
        text.AppendLine(
            "I use Dependinator, a tool that maps the structure and dependencies of a codebase. It found a "
                + "circular dependency, and I need to decide whether to accept it as intended (with a short "
                + "reason that is stored with the model) or to break it."
        );
        if (Codebase(container) is { } codebase)
            text.AppendLine().AppendLine(codebase);

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

        text.AppendLine().Append(TaskSection);
        return text.ToString();
    }

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

    // The closest node that is not a member: a member's type, or the node itself.
    static Node Unit(Node node) => node.AncestorsAndSelf().First(n => !n.Type.IsMember);

    // The member directly under the node's type (a getter or a local function counts as its
    // property or method), or the node itself when it is not a member.
    static Node Member(Node node)
    {
        var unit = Unit(node);
        while (node != unit && node.Parent != unit)
            node = node.Parent;
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
