using System.Text;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.PromptParts;
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
static class CyclePrompt
{
    const int MaxReasonLength = 200;
    const int MaxSourceUnitsPerDirection = 2;

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
        // The prompt may be pasted into an AI that can change the code (Claude Code, say), which
        // would take the asked-for change as a task; reading the code makes the verdict better.
        text.AppendLine()
            .AppendLine(
                "This is a review only: do not change, create or commit any files, and do not run commands "
                    + "that do. If you can read the code, use it to check your answer."
            );
        if (Codebase(container) is { } codebase)
            text.AppendLine().AppendLine(codebase);
        AppendCycle(text, cycle, container, members);

        if (sources is { Count: > 0 })
            AppendSource(
                text,
                model,
                sources,
                "The code behind the directions made by only one or two types, the ones most likely to be broken or to be explained."
            );

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

    static readonly string TaskSection = $"""
        ## Your task

        Decide whether this cycle should be accepted as intended or broken. Base the decision on the dependencies above: how many links go each way, what they are for, and whether the members are really one concept.

        Answer in this format:

        Verdict: Accept or Break
        Why: 2–4 sentences.
        Assumptions: what you assumed about code that is not shown here, or "None".
        Reason to store: (only if Accept) at most {MaxReasonLength} characters, specific to these members, for a future maintainer; not a general statement about cycles.
        Change: (only if Break) the smallest concrete change that removes one direction, and which direction (describe it; do not make it).

        If the information is not enough to decide, say what you would need to see instead of guessing.

        """;

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
}
